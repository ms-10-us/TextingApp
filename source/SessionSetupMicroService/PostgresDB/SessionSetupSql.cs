namespace SessionSetupMicroService.PostgresDB
{
    /// <summary>
    /// Every statement this service sends, in one place.
    ///
    /// Not tidiness for its own sake: it means the full set of queries can be reviewed as a unit, and
    /// each one can be <c>PREPARE</c>d against a live schema by tools/verify-sql.sh — which turns a
    /// renamed column into a failing build step instead of a 500 in production.
    ///
    /// Enum parameters travel as text and are cast in SQL (<c>@kind::session_setup.prekey_kind</c>) so
    /// the data layer needs no provider-specific enum mapping.
    /// </summary>
    public static class SessionSetupSql
    {
        // --- accounts and devices ------------------------------------------------

        public const string EnsureAccount = """
        insert into session_setup.accounts (account_id)
        values (@account_id)
        on conflict (account_id) do nothing
        """;

        /// <summary>
        /// Takes a row-level lock on the account, held until the transaction commits.
        ///
        /// This is what makes device linking safe. NextDeviceId is max(device_id) + 1, and under
        /// ReadCommitted two concurrent links both read the same max, both compute the same id, and
        /// the second insert dies on the primary key. Locking the account row first makes the second
        /// link queue behind the first and read the committed maximum.
        ///
        /// EnsureAccount cannot do this job: `insert ... on conflict do nothing` takes no lock on a
        /// row that already exists.
        ///
        /// Lock order across the service is account -> device -> prekeys. Registration, linking and
        /// publishing all follow it, which is why none of them can deadlock against another.
        /// </summary>
        public const string LockAccount = """
        select 1
        from session_setup.accounts
        where account_id = @account_id
        for update
        """;

        /// <summary>
        /// `on conflict do nothing` so a duplicate is reported by the row count rather than by an
        /// exception. That keeps the Npgsql error code out of the orchestrator: the repository
        /// returns false, and the use case decides that false means DeviceAlreadyRegistered.
        /// </summary>
        public const string InsertDevice = """
        insert into session_setup.devices
            (account_id, device_id, display_name, registration_id,
             identity_algorithm, identity_key, credential_hash)
        values
            (@account_id, @device_id, @display_name, @registration_id,
             @identity_algorithm, @identity_key, @credential_hash)
        on conflict (account_id, device_id) do nothing
        """;

        public const string FindDevice = """
        select account_id, device_id, display_name, registration_id,
               identity_algorithm, identity_key, credential_hash, registered_at, last_seen_at
        from session_setup.devices
        where account_id = @account_id and device_id = @device_id
        """;

        public const string ListDevicesByAccount = """
        select account_id, device_id, display_name, registration_id,
               identity_algorithm, identity_key, credential_hash, registered_at, last_seen_at
        from session_setup.devices
        where account_id = @account_id
        order by device_id
        """;

        public const string DeviceExists = """
        select exists (
            select 1 from session_setup.devices
            where account_id = @account_id and device_id = @device_id
        )
        """;

        public const string GetCredentialHash = """
        select credential_hash
        from session_setup.devices
        where account_id = @account_id and device_id = @device_id
        """;

        public const string TouchDevice = """
        update session_setup.devices
        set last_seen_at = now()
        where account_id = @account_id and device_id = @device_id
        """;

        /// <summary>
        /// Only correct while the account row is locked — see <see cref="LockAccount"/>.
        /// </summary>
        public const string NextDeviceId = """
        select coalesce(max(device_id), 0) + 1
        from session_setup.devices
        where account_id = @account_id
        """;

        // --- signed prekeys ------------------------------------------------------

        public const string UpsertSignedPreKey = """
        insert into session_setup.signed_prekeys
            (account_id, device_id, kind, key_id, public_key, signature)
        values
            (@account_id, @device_id, @kind::session_setup.prekey_kind, @key_id, @public_key, @signature)
        on conflict (account_id, device_id, kind) do update
        set key_id     = excluded.key_id,
            public_key = excluded.public_key,
            signature  = excluded.signature,
            created_at = now()
        """;

        public const string GetSignedPreKey = """
        select account_id, device_id, kind::text, key_id, public_key, signature, created_at
        from session_setup.signed_prekeys
        where account_id = @account_id
          and device_id  = @device_id
          and kind       = @kind::session_setup.prekey_kind
        """;

        // --- one-time prekeys ----------------------------------------------------

        /// <summary>
        /// Bulk insert in a single round trip. <c>on conflict do nothing</c> makes an upload idempotent:
        /// a client retrying after a timeout re-sends the same ids and converges, instead of erroring or
        /// duplicating.
        /// </summary>
        public const string AddOneTimePreKeys = """
        insert into session_setup.one_time_prekeys
            (account_id, device_id, kind, key_id, public_key, signature)
        select @account_id, @device_id, @kind::session_setup.prekey_kind,
               k.key_id, k.public_key, nullif(k.signature, ''::bytea)
        from unnest(@key_ids::bigint[], @public_keys::bytea[], @signatures::bytea[])
             as k (key_id, public_key, signature)
        on conflict (account_id, device_id, kind, key_id) do nothing
        """;

        /// <summary>
        /// Pop exactly one unused key, atomically. The correctness of the whole layer rests on this
        /// statement, so it is worth reading closely:
        ///
        ///   FOR UPDATE SKIP LOCKED   two concurrent fetches lock and take DIFFERENT rows rather than
        ///                            one blocking on the other, so contention costs nothing and neither
        ///                            caller can observe the other's row.
        ///   DELETE ... RETURNING     consumption and read are one statement, so there is no window in
        ///                            which a key has been read but not yet marked used.
        ///   ctid                     the physical row the subquery locked, so the delete removes
        ///                            exactly that row and not merely one like it.
        /// </summary>
        public const string TakeOneTimePreKey = """
        delete from session_setup.one_time_prekeys
        where ctid = (
            select ctid
            from session_setup.one_time_prekeys
            where account_id = @account_id
              and device_id  = @device_id
              and kind       = @kind::session_setup.prekey_kind
            order by key_id
            for update skip locked
            limit 1
        )
        returning account_id, device_id, kind::text, key_id, public_key, signature, created_at
        """;

        public const string CountOneTimePreKeys = """
        select kind::text, count(*)
        from session_setup.one_time_prekeys
        where account_id = @account_id and device_id = @device_id
        group by kind
        """;
    }

}
