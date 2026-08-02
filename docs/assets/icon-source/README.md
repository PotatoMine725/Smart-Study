# App icon — design sources

Source artwork for the application icon. **Not referenced by the build.**

The icon the build actually ships is `SmartStudyPlanner/Assets/icon.ico`,
referenced twice from `SmartStudyPlanner/SmartStudyPlanner.csproj`
(`<ApplicationIcon>` and `<Resource Include>`, both project-relative).

If you change the artwork here, re-export `icon.ico` and update that file —
copying into this directory alone changes nothing.

| File | Role |
|---|---|
| `icon.svg` | Master vector |
| `favicon.svg` | Favicon variant |
| `png/icon-{16,32,48,256}.png` | Raster exports |
| `Icon Preview.html` | Local preview sheet |

## Where these came from

Until 2026-08-02 they lived in an **untracked** root `Assets/` directory, which
nothing in the build referenced — the CSA of 2026-07-27 recorded that folder as a
build dependency, and that was wrong (see its Corrections section). Its `icon.ico`
was byte-identical (md5 `d2bc90edcb01d398ce82ded7ff497177`) to the tracked copy, so
only these design sources were unique, and only they were worth preserving. They are
committed here so a fresh clone keeps them.
