#!/usr/bin/env bash
# Regenerates the raw P/Invoke layer from the header build-native.sh produced.
#
# Hand-writing libsignal's full FFI surface is how interop bugs ship: a wrong struct layout or a
# missed error-pointer return corrupts memory instead of failing cleanly. Generate it, review the
# diff, and hand-write only the safe wrappers in Interop/SignalFfi.cs.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
HEADER="$ROOT/native/signal_ffi.h"
OUT="$ROOT/src/SessionSetup.Protocol/Interop/NativeMethods.g.cs"

[ -f "$HEADER" ] || { echo "Missing $HEADER — run build-native.sh first." >&2; exit 1; }

dotnet tool install -g ClangSharpPInvokeGenerator 2>/dev/null || true
export PATH="$PATH:$HOME/.dotnet/tools"

ClangSharpPInvokeGenerator \
  --file "$HEADER" \
  --output "$OUT" \
  --namespace SessionSetup.Protocol.Interop \
  --methodClassName NativeMethodsGenerated \
  --libraryPath signal_ffi \
  --config latest-codegen generate-macro-bindings multi-file exclude-fnptr-codegen \
  --language c --std c17

echo "==> wrote $OUT"
echo "Review the diff, then delete anything in NativeMethods.cs the generated file replaces."
