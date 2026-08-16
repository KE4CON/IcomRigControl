# User Manual — build pipeline (planned)

The User Manual is the second of the three core documents. It covers **every feature
to the house standard** — window by window, every field and button named exactly,
each feature chapter ending in a troubleshooting section, plus reference chapters
(Glossary, Keyboard Shortcuts, Troubleshooting & FAQ). It is a Word document **with
screenshot placeholders**.

Until this pipeline is built out, the current functional reference is
[`UserManual.md`](../../UserManual.md) at the repository root.

When built, this folder mirrors the Installation Guide pipeline:

```
user-manual/
  build.py          -> ../published/IcomRigControl_User_Manual.docx
  chapters/*.json   One JSON file per feature chapter (the living source of truth).
```

It imports the shared house style from `../docs_generators/style.py`.
