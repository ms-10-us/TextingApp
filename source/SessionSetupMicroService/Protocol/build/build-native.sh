#!/usr/bin/env bash
# Builds signalapp/libsignal's C FFI and emits the shared library plus the generated header.
#
# Requires: rust toolchain, cmake, clang, protobuf-compiler, git.
# Produces: native/libsignal_ffi.{so,dylib} (or signal_ffi.dll) and native/signal_ffi.h
set -euo pipefail

# Pin a released tag. Never track main: the FFI surface changes between releases and generated
# interop must match the binary you actually ship.
LIBSIGNAL_TAG="${LIBSIGNAL_TAG:-v0.86.4}"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SRC="$ROOT/.libsignal-src"
OUT="$ROOT/native"
mkdir -p "$OUT"

if [ ! -d "$SRC" ]; then
  git clone --depth 1 --branch "$LIBSIGNAL_TAG" https://github.com/signalapp/libsignal.git "$SRC"
else
  git -C "$SRC" fetch --depth 1 origin tag "$LIBSIGNAL_TAG"
  git -C "$SRC" checkout -q "$LIBSIGNAL_TAG"
fi

echo "==> building libsignal-ffi ($LIBSIGNAL_TAG)"
( cd "$SRC" && cargo build --release -p libsignal-ffi )

echo "==> generating signal_ffi.h"
cargo install --quiet cbindgen --version '^0.27' 2>/dev/null || true
( cd "$SRC/rust/bridge/ffi" && cbindgen --profile release -o "$OUT/signal_ffi.h" )

case "$(uname -s)" in
  Darwin) cp "$SRC/target/release/libsignal_ffi.dylib" "$OUT/" ;;
  Linux)  cp "$SRC/target/release/libsignal_ffi.so"    "$OUT/" ;;
  *)      cp "$SRC/target/release/signal_ffi.dll"      "$OUT/" ;;
esac

echo "==> done. Set SIGNAL_FFI_PATH=$OUT"
ls -la "$OUT"
