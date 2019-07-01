# UlidSharp
Performance oriented Ulid implementation of https://github.com/ulid/spec for .Net Core

Use the `ref ulong rndState` overrides to avoid contention of s_rndState when generating ulids concurrently.
