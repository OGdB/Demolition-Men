// NativeWebSocket's jslib calls Module.dynCall_vi / _vii / _viii. This Unity's Emscripten doesn't put
// those on Module, and the package's own jspre skips them when getWasmTableEntry isn't in the build,
// so the socket's first callback (onopen) throws. A callback pointer is an index in the wasm table.
Module.dynCall_vi = Module.dynCall_vi || function (cb, a) { return wasmTable.get(cb)(a); };
Module.dynCall_vii = Module.dynCall_vii || function (cb, a, b) { return wasmTable.get(cb)(a, b); };
Module.dynCall_viii = Module.dynCall_viii || function (cb, a, b, c) { return wasmTable.get(cb)(a, b, c); };
