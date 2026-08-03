# CI smoke check

This fixture exists purely so `tools/SmokeTest` has something deterministic
to render in CI. It exercises the same code paths as `.charter/capabilities/markdown-rendering.md`
without depending on files outside the repo.

- a list
- **bold**, *italic*, `code`

```text
a fenced code block
```

| A | B |
| - | - |
| 1 | 2 |
