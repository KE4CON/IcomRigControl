# Programming Guide — build pipeline (planned)

The Programming Guide is the third of the three core documents: a developer/maintainer
code book that explains **how AND why** the code works. Every section answers *What it
does → Why it was built this way → How it works*, grounded in the real source with
exact code excerpts. It is the one core document with **no screenshots**.

When built, this folder mirrors the Installation Guide pipeline:

```
programming-guide/
  build.py          -> ../published/IcomRigControl_Programming_Guide.docx (+ .md mirror)
  chapters/*.json   One JSON file per chapter (the living source of truth).
```

It imports the shared house style from `../docs_generators/style.py`.
