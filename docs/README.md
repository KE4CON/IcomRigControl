# IcomRigControl — Documentation

This folder holds all of the project's documentation and the pipeline that builds
the styled Word/PDF deliverables. It is organized into **public** material (things
end users and contributors are meant to see) and an **internal** area (engineering
notes end users don't need).

## Layout

```
docs/
  docs_generators/          Shared house style (style.py — navy + gold python-docx).
                            Every guide's build.py imports it, so the three core
                            documents look and read the same.

  installation-guide/       Installation Guide — build pipeline.
    build.py                  Builds ../published/IcomRigControl_Installation_Guide.docx
    chapters/*.json           One JSON file per chapter (the living source of truth).
    *.md                      Markdown mirror, regenerated on every build.

  user-manual/              User Manual — build pipeline (per-feature depth). PLANNED.
    chapters/                 One JSON file per feature chapter.

  programming-guide/        Programming Guide — build pipeline (the code book, NO
    chapters/               screenshots). PLANNED.

  published/                The finished, shareable deliverables (.docx / .pdf).
                            This is what you hand to a user.

  internal/                 Engineering docs end users don't need to see.
    audit/                    AUDIT_METHODS.md and audit findings ledgers.
    reference/                CI-V reference PDFs, the CAPABILITIES inventory.
    TESTING_CHECKLIST.md      The manual test checklist.
```

## The three core documents (house standard)

Every project ships three: **Installation Guide**, **User Manual**, and
**Programming Guide**. Each is built by the same pipeline — Markdown/JSON is the
living source of truth, and the styled `.docx` (and PDF) is generated from
per-chapter JSON by that guide's `build.py` using the shared `docs_generators/style.py`.

- The **Installation Guide** and **User Manual** are Word documents **with screenshot
  placeholders**, so real screenshots can be dropped in.
- The **Programming Guide** is a code book with **no screenshots**.

## Building a guide

```bash
cd docs/installation-guide
python build.py
```

Requires Python 3 with `python-docx` (`pip install python-docx`). Output lands in
`docs/published/`.

## What stays at the repository root

- `CLAUDE.md` — the project rules and phase status (developer/AI facing).
- `README.md` — the repository README.
- `UserManual.md` — the current, functional end-user manual (the living reference
  until the `docs/user-manual/` pipeline supersedes it); kept at the root because the
  project discipline in `CLAUDE.md` references it there.
