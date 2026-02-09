# Live Crowdsourced Data - Feature Branch

## Purpose
This branch contains the development of the live crowdsourced terminal data feature.

## Status
🚧 **In Development** - Phase 1: OCR Foundation

## Branch Strategy
- **Main Branch (`master`)**: Stable releases, bug fixes only
- **This Branch (`feature/live-crowdsourced-data`)**: Live data development
- **Merge Strategy**: Only merge to master when fully tested and ready

## Current Phase
**Phase 1: OCR Foundation (Weeks 1-2)**
- [ ] Add Tesseract OCR NuGet package
- [ ] Implement game window detection
- [ ] Capture terminal screen region
- [ ] Parse OCR text into structured data
- [ ] Create UI toggle for data sharing

## How to Work on This
1. **For bug fixes on released app**: Switch to `master` branch
2. **For live data feature**: Stay on this branch
3. **Testing**: Keep both branches to compare

## Next Steps
1. Start Phase 1 implementation
2. Keep master branch for v1.1.9+ releases
3. Merge this feature when ready (estimated 7 weeks)

---
See `live_data_implementation_plan.md` for full details.
