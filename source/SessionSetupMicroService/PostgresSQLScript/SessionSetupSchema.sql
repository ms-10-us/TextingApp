-- =============================================================================
-- 001_session_setup
--
-- Storage for PQXDH session setup: devices, the signed prekeys senders verify
-- against, and the pools of one-time keys they consume.
--
-- One rule shapes this schema: a one-time prekey must be issued to at most one
-- sender, ever. Issuing the same one twice raises no error, corrupts nothing
-- visible, and quietly weakens the forward secrecy of two conversations. A bug
-- that silent has to be impossible by construction, and the database is the only
-- place that can make it so across concurrent requests and multiple instances.
--
-- Every object is schema-qualified rather than relying on search_path: this
-- script runs both from the application's migrator and by hand through psql, and
-- an unqualified name landing in `public` because a SET did not take is a silent
-- and confusing failure.
-- =============================================================================

create schema if not exists session_setup;

-- --- accounts and devices ----------------------------------------------------

create table if not exists session_setup.accounts (
    account_id  uuid        primary key,
    created_at  timestamptz not null default now()
);

-- One row per device. Each holds its OWN identity key and its own prekeys: a
-- sender opening a conversation with a person runs one PQXDH handshake per
-- device that person owns, so nothing here is shared across an account.
create table if not exists session_setup.devices (
    account_id          uuid        not null references session_setup.accounts (account_id) on delete cascade,
    device_id           integer     not null,
    display_name        text        not null,
    -- 14-bit, per the Signal wire format. Mixed into session state so a reinstall
    -- is detectable rather than looking like a device that has gone quiet.
    registration_id     integer     not null,
    identity_algorithm  text        not null,
    identity_key        bytea       not null,
    -- Only the hash is stored: the credential itself is returned once, at
    -- registration, and cannot be recovered from here.
    credential_hash     bytea       not null,
    registered_at       timestamptz not null default now(),
    last_seen_at        timestamptz not null default now(),

    primary key (account_id, device_id),
    constraint devices_device_id_positive     check (device_id >= 1),
    constraint devices_registration_id_14bit  check (registration_id between 0 and 16383),
    constraint devices_display_name_length    check (char_length(display_name) between 1 and 128),
    constraint devices_identity_key_ed25519   check (octet_length(identity_key) = 32),
    constraint devices_credential_hash_sha256 check (octet_length(credential_hash) = 32)
);

create index if not exists devices_account_idx
    on session_setup.devices (account_id, device_id);

-- --- prekeys -----------------------------------------------------------------

-- The two halves of the PQXDH hybrid. They share tables because their lifecycles
-- are identical; only the sizes and the signing rules differ.
do $$ begin
    create type session_setup.prekey_kind as enum ('curve', 'kyber');
exception when duplicate_object then null;
end $$;

-- Keys that are NOT consumed by a fetch: the rotating signed curve prekey, and
-- the reusable last-resort Kyber prekey. The primary key permits exactly one
-- current key of each kind per device, so rotation is an upsert rather than an
-- insert plus a cleanup job that someone eventually forgets to run.
create table if not exists session_setup.signed_prekeys (
    account_id  uuid                      not null,
    device_id   integer                   not null,
    kind        session_setup.prekey_kind not null,
    key_id      bigint                    not null,
    public_key  bytea                     not null,
    signature   bytea                     not null,
    created_at  timestamptz               not null default now(),

    primary key (account_id, device_id, kind),
    foreign key (account_id, device_id)
        references session_setup.devices (account_id, device_id) on delete cascade,
    constraint signed_prekeys_key_id_positive check (key_id >= 0),
    constraint signed_prekeys_signature_ed25519 check (octet_length(signature) = 64),
    -- X25519 is 32 bytes, Kyber-1024 encapsulation keys are 1568. Enforcing the
    -- pairing here means a key of the wrong family cannot be stored under the
    -- wrong kind, whatever the application layer believes.
    constraint signed_prekeys_size_matches_kind check (
        (kind = 'curve' and octet_length(public_key) = 32) or
        (kind = 'kyber' and octet_length(public_key) = 1568)
    )
);

-- The consumable pool. A row's presence IS the fact that the key is unused:
-- consumption is a DELETE, so there is no `used` flag for a future query to
-- forget to filter on.
create table if not exists session_setup.one_time_prekeys (
    account_id  uuid                      not null,
    device_id   integer                   not null,
    kind        session_setup.prekey_kind not null,
    key_id      bigint                    not null,
    public_key  bytea                     not null,
    -- Kyber one-time keys are individually signed; curve one-time keys are
    -- authenticated transitively through the signed prekey in the same bundle.
    signature   bytea,
    created_at  timestamptz               not null default now(),

    primary key (account_id, device_id, kind, key_id),
    foreign key (account_id, device_id)
        references session_setup.devices (account_id, device_id) on delete cascade,
    constraint one_time_prekeys_key_id_positive check (key_id >= 0),
    constraint one_time_prekeys_size_matches_kind check (
        (kind = 'curve' and octet_length(public_key) = 32) or
        (kind = 'kyber' and octet_length(public_key) = 1568)
    ),
    -- The `signature is not null` half is load-bearing, not belt-and-braces. With a null
    -- signature, octet_length(signature) is null, so the constraint would evaluate to
    -- (false or null) = null — and a CHECK that evaluates to null PASSES. Without the explicit
    -- null test, an unsigned Kyber prekey slips straight through the very check meant to stop it.
    constraint one_time_prekeys_kyber_is_signed check (
        kind <> 'kyber' or (signature is not null and octet_length(signature) = 64)
    )
);

-- Supports the pop: oldest unused key of one kind for one device.
create index if not exists one_time_prekeys_pop_idx
    on session_setup.one_time_prekeys (account_id, device_id, kind, key_id);
