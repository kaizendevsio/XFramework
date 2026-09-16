"""Test-only adapter to exercise the production native ABI with the browser library."""
import ctypes
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[1]
name = 'xframework_opaque.dll' if sys.platform == 'win32' else 'libxframework_opaque.so'
lib = ctypes.CDLL(str(root / 'target' / 'release' / name))
lib.xfw_opaque_execute.argtypes = [ctypes.c_char_p]
lib.xfw_opaque_execute.restype = ctypes.c_void_p
lib.xfw_opaque_free.argtypes = [ctypes.c_void_p]
for line in sys.stdin:
    pointer = lib.xfw_opaque_execute(line.encode('utf-8'))
    try:
        print(ctypes.string_at(pointer).decode('utf-8'), flush=True)
    finally:
        lib.xfw_opaque_free(pointer)
