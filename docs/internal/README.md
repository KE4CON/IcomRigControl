# Internal documentation

Engineering and project-maintenance docs that **end users don't need to see**. None
of this ships to users; it supports developing, testing, and releasing the app.

- `audit/` — `AUDIT_METHODS.md` (the portable 12-method audit catalog) and its
  Findings Ledger.
- `reference/` — reference material used while building the app: the Icom CI-V
  reference PDFs, and `CAPABILITIES.md` (the full radio/feature inventory and the
  tier analysis behind the three-radio scope decision).
- `TESTING_CHECKLIST.md` — the manual, hands-on test checklist.

Public, user-facing material lives one level up: the build pipelines
(`installation-guide/`, `user-manual/`, `programming-guide/`) and the finished
deliverables in `published/`.
