# Copilot Instructions

## Project Guidelines
- In this project, DCM file names are random and should never be used to infer tooth/crown classification.
- For the DCMViewer workflow, the initial viewport zoom indicator should start at 100% (no startup over/under-zoom behavior). Revert all custom zoom behavior changes. Prefer focused fixes only; do not change unrelated UI colors when asked to fix functional issues like clipping.
- Texture mapping should remain enabled rather than being dropped by strict UV confidence rejection. Disable texture rendering entirely for now because current texture mapping is not working.
- For failed scans in the file list, the filename badge should use a clearly maroon background (not bluish).
- Prefer MVVM: keep code-behind minimal and move logic from code-behind to ViewModel or dedicated components/services wherever feasible. Use RelayCommand bindings for UI actions.