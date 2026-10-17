# Installation

Download **Polyus-Demo-0.2.unitypackage**, then in your existing Unity 6.6
Universal 2D project choose **Assets → Import Package → Custom Package**.
Import all selected assets. The welcome window opens the included demo scene.
Press **Play** and start a run. No manual file selection or object wiring needed.

The package includes scripts, art, audio, scene, editor installer, and tests.
It updates earlier Polyus assets using their original GUIDs and does not include
ProjectSettings or overwrite the user's Game/SampleScene. Back up local work
before importing. Unity Input System, uGUI and Test Framework are prerequisites
already included in this repository's Universal 2D manifest.

This is a playable-demo source package, not an APK/IPA or store-ready release.
It has been structurally verified; Unity import and runtime checks remain to
be executed in the Editor because Unity is not available in the cloud workspace.

Rebuild: `python Tools/package_unity.py`. The script verifies every packaged
asset and metadata payload against the current source tree.
