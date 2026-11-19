"""SHA-pinned evidence for previously reviewed branch retirements."""

# Explicit one-time retirements stay SHA-pinned. Ordinary merged
# historical heads require their own exact-SHA verify evidence.
# The old rescue/guard branches used different ORK UI wiring.
# Their accepted behavior now resides in the verified turn/gate/
# guard/attack/escort path, including the real Play Mode battle.
# Pin the entire touched replacement surface, not just a label.
LEGACY_ROLE_BLOBS={
    "Assets/Game/Editor/SoundwellLocalizationSetup.cs":
        "6d2d03d10b56b4d1832efbe977e8f5bc20ee7ce9",
    "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-Shared.asset":
        "7a7441a0981858308e9f610918da8670ace8dab5",
    "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-en.asset":
        "05101562a41e5795ef0ce855e361e2e49ff129d6",
    "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-zh-Hans.asset":
        "93d53e856e2f73ea6d071b6ad9f582fd78b221b3",
    "Assets/Game/Soundwell/SoundwellOrkParty.cs":
        "4cab8c26568cb0f426307d272699ffeceea89811",
    "Assets/Game/Soundwell/SoundwellTrial.cs":
        "c47416e6253b3398a70098bce45ab880c6b2269d",
    "Assets/Game/Soundwell/SoundwellView.cs":
        "1f4ebd3db1f26e4eaca7d54230d5297e5cedc424",
    "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
        "644b493d5bd1239ceb80dd913123d3c3b0f7b550",
    "Assets/Game/Tests/Editor/SoundwellTrialTests.cs":
        "0539f8f8048f623a4d80547b54860ed2dce5bc90",
    "tools/collect_unity_p0.py":
        "3fbd2169d1c1e4c74ae8fd99fc252ba1da0da8a1",
    "tools/test_collect_unity_p0.py":
        "297b81a0c2f7523e680a53efc7a990e0aa7dbb28",
}
LEGACY_GUARD_M4_M10_BLOBS={
    "Assets/Game/Editor/SoundwellAnimationSetup.cs":
        "2be25f614b88eeec34a0739a2d8c098e235b4895",
    "Assets/Game/Soundwell/SoundwellMemoryProbe.cs":
        "cfca327191f3426894a2a83043e39c66fa7b113a",
    "Assets/Game/Soundwell/SoundwellOrkParty.cs":
        "4cab8c26568cb0f426307d272699ffeceea89811",
    "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
        "644b493d5bd1239ceb80dd913123d3c3b0f7b550",
}
ARCHIVED={
    "codex/pending/m04-roster-reconciliation-destroyed-guard-nov19": {
        "head":"7e013e82e54c37be44389aa94e075810dd562f50",
        "verified":"22c8b262efc736f3f1b293cc2680810fdf1b6106",
        "run":44561748568,
        "original_run":44562172577,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"Merged PR 196 keeps all teardown and locale guards and strengthens the regression with a factual roster count assertion.",
        "blobs":{
            "Assets/Game/Soundwell/M04RosterReconciliationView.cs":"c4a8370866d1879272ef71ac9ad5debc502a0944",
            "Assets/Game/Tests/Editor/M04RosterReconciliationViewTests.cs":"6edcaa5a85b2421b28e7381c945718feb086c6e7",
        },
    },
    "codex/pending/m04-xiaodou-farewell-nov18": {
        "head":"02325af7e0a9388b11f9e4dd3121fa9a6e47ed9b",
        "verified":"624466f9833932031a316d4c6f153d80330fec6d",
        "run":44454741200,
        "original_run":44417323573,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"The verified regression exercises both prior prompt histories in one NUnit case and adds the bilingual crossing and main cable checks.",
        "blobs":{
            "Assets/Game/Tests/Editor/M04XiaodouFarewellTests.cs":"d3f8c340b0359b812fd5158070a619098a361cc7",
        },
    },
    "codex/pending/m00-memorial-entry-lifecycle-nov17": {
        "head": "72b52fc9bc3357b7ecc3c51a153e5b232931ad14",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement retains the live Entry owner, direct memorial handoff, responsive layout, authored titles, and outcome validation through the full harbor flow. The experimental test-only preference catch and manual DLL cleanup are superseded by a fresh verified checkout and reload-safe test.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "f1317792ff1f6f52ea86e4b93b09673df63913b7",
            "Assets/Game/Soundwell/SoundwellUiLocalization.cs": "9cf29b47e76b7ceecf49fab043c28cdd5824c0fe",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "a1fb9435b69026f144641c26dca750aefe3bcf4b",
            "production/ci/unity-with-p0-batch.sh": "556dd452ba89394bd486dc4c5ca6eee776cf58f4",
            "tools/test_collect_unity_p0.py": "e6615a7c2647e4185a42e65f903b9882dcd2ff4a",
            ".github/workflows/unity-p0-editor.yml": "14dc8c2f9f21087ed2b122877f501805cf40cdf6",
            "tools/restore_unity_p0.py": "a2a681f09f5c7b5541d663a492118029ea90e4ba",
        },
    },
    "codex/pending/m00-responsive-authored-view-nov16": {
        "head": "c828f0f19bdc6c91e02fd8c1c014a58d4c3294e5",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement retains the same viewport fitting and bounds coverage through the live Entry owner and the current multi-scene opening.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
        },
    },
    "codex/pending/m00-responsive-real-entry-nov18": {
        "head": "e8e0bcdc6d73b629a9659ec9bf86af973f1f2f8d",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "original_run": 44343113169,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement retains the same viewport fitting and bounds coverage through the live Entry owner and the current multi-scene opening.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "f1317792ff1f6f52ea86e4b93b09673df63913b7",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
        },
    },
    "codex/pending/m00-responsive-view-clean-ci-nov17-r3": {
        "head": "55c2edc41c1639c2e6cc67432f03b5df6c5f1b77",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement retains the responsive layout and authored titles through a reload-safe live-entry test. Current clean-run verification supersedes the experimental manual assembly purge.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "a1fb9435b69026f144641c26dca750aefe3bcf4b",
            "production/ci/unity-with-p0-batch.sh": "556dd452ba89394bd486dc4c5ca6eee776cf58f4",
            "tools/test_collect_unity_p0.py": "e6615a7c2647e4185a42e65f903b9882dcd2ff4a",
            ".github/workflows/unity-p0-editor.yml": "14dc8c2f9f21087ed2b122877f501805cf40cdf6",
            "tools/restore_unity_p0.py": "a2a681f09f5c7b5541d663a492118029ea90e4ba",
        },
    },
    "codex/pending/m00-responsive-view-clean-ci-nov17": {
        "head": "bbba6ea5f28d896f38dc8ee48a164a8949b81f45",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement retains the responsive layout and authored titles through a reload-safe live-entry test. Current clean-run verification supersedes the experimental manual assembly purge.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "a1fb9435b69026f144641c26dca750aefe3bcf4b",
            "production/ci/unity-with-p0-batch.sh": "556dd452ba89394bd486dc4c5ca6eee776cf58f4",
            ".github/workflows/unity-p0-editor.yml": "14dc8c2f9f21087ed2b122877f501805cf40cdf6",
            "tools/restore_unity_p0.py": "a2a681f09f5c7b5541d663a492118029ea90e4ba",
        },
    },
    "codex/pending/v12-remove-scene-id-player-titles-nov16": {
        "head": "0e65f38a9fba73ba8c6e89952c7ab30c20f45875",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement uses authored S00 and M00 titles in English and Chinese and retains the localized title and live opening regression.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
        },
    },
    "codex/pending/v12-scene-title-regression-followup-nov16": {
        "head": "0ef1433880f1921c9e961571a5b3d250442ffa2a",
        "verified": "b3a917b48617f541c83edb6a75d1b4fe205c75c5",
        "run": 44370360856,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The verified replacement uses authored S00 and M00 titles in English and Chinese and retains the localized title and live opening regression.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "2afb72a74cdefb24ede0a6d210157f906e08460d",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "1d4334606c8e7de0ff00cc1cfea9cffd58f4cf56",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "e80b9416b5b4295247719335efe8cdfec37c9685",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "a1fb9435b69026f144641c26dca750aefe3bcf4b",
        },
    },
    "codex/pending/guilai-v1-canon-conflict-resolution-nov16": {
        "head": "e181d8234031534254f7103cfbbe1dab188a23fc",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "archive_tag": "refs/tags/archive/guilai-v1/canon-conflict-resolution-nov16",
        "reason": "Retire non-canonical v1.0 working files after the v1.2 handoff. Archive the exact original commit before removing its pending branch; unique old proposals remain retrievable.",
        "blobs": {
            "docs/gameplay/BASELINE.md": "ef89ade52c804a272c79ffbd5e34736fec3fb577",
            "docs/narrative/STORY_OUTLINE.md": "2b8ac5d8e983c712d5449b5886729c6fda1d2672",
            "docs/narrative/NARRATIVE_FACTS.md": "b625155cc149df2c141f0d9192b822b8bbaedc9d",
            "docs/narrative/HANDOFF_FOR_CODEX.md": "cacc21c91dcaf4a90e777d9ed7fa5ef692cb495d",
        },
    },
    "codex/pending/guilai-v1-consistency-nov16": {
        "head": "575a4eaaec6564424eee552d83d80014bb4759f5",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "archive_tag": "refs/tags/archive/guilai-v1/consistency-nov16",
        "reason": "Retire non-canonical v1.0 working files after the v1.2 handoff. Archive the exact original commit before removing its pending branch; unique old proposals remain retrievable.",
        "blobs": {
            "docs/gameplay/BASELINE.md": "ef89ade52c804a272c79ffbd5e34736fec3fb577",
            "docs/narrative/STORY_OUTLINE.md": "2b8ac5d8e983c712d5449b5886729c6fda1d2672",
            "docs/narrative/NARRATIVE_FACTS.md": "b625155cc149df2c141f0d9192b822b8bbaedc9d",
            "docs/narrative/HANDOFF_FOR_CODEX.md": "cacc21c91dcaf4a90e777d9ed7fa5ef692cb495d",
        },
    },
    "codex/pending/m00-han-ransom-facts-nov18": {
        "head": "8dbf06d72bbfd28c106c0edb6dd14f8681568bee",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44257090559,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered meeting records the canonical forty-one at the authored reveal and retains the PlayMode persistence and legacy-save test.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanRansomFacts.cs": "21e2c7f6121f7e01b9dbf87dbea18c88419b6a4d",
            "Assets/Game/Soundwell/M00HanRansomFacts.cs.meta": "6b1578d7d1111dd38f38ab04dd8941b5c329113c",
            "Assets/Game/Tests/Editor/M00HanRansomFactsTests.cs": "47dfa008e2a8305d3f5ccfb7559735ba9e63e593",
            "Assets/Game/Tests/Editor/M00HanRansomFactsTests.cs.meta": "940e58e4de0e4b742b52827460a2f2aa34f685c3",
            "tools/collect_unity_p0.py": "797d146c14d83f466887f37a865e9de51ac0e23f",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m00-hanqiao-meeting-view-nov18": {
        "head": "68ab462b1f063834358fb4c043b96ee7984add10",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44257867743,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered bilingual view keeps all authored meeting choices and regressions, adds confirmation at the ransom reveal, and continues to West Lane.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanQiaoMeetingView.cs": "6c405c783d9ede48539ed3ddbd755b119ac6b48f",
            "Assets/Game/Soundwell/M00HanQiaoMeetingView.cs.meta": "9cef7fe73094dd7b6c69c9d88913d4c6b4b45b0b",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingViewTests.cs": "2082f598a7fac3fdc51622dcd9a85b0f55043fa5",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingViewTests.cs.meta": "b75e350650caed9d2000a699766c7098ba129849",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m00-rollcall-session-nov18": {
        "head": "06914e80f52b42c4a998bc72deac6fe3e73f8de8",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44256911336,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered three-call session keeps its timing, input choices, persisted outcome and regressions, with focus and view teardown in the actual route.",
        "blobs": {
            "Assets/Game/Soundwell/M00RollCallSession.cs": "a92dc4996184bf0c510f504d2d1e2857bfce281b",
            "Assets/Game/Soundwell/M00RollCallSession.cs.meta": "53d0838dd57400f7e5ddbf2fbf8fa86197f496ce",
            "Assets/Game/Soundwell/M00RollCallView.cs": "15bf26c8193eae1c4f5b7b0ceb8f202f9a6f02f1",
            "Assets/Game/Tests/Editor/M00RollCallSessionTests.cs": "7590e1ea97ed64ad650aaa83a9770917ac32a919",
            "Assets/Game/Tests/Editor/M00RollCallSessionTests.cs.meta": "a7195a91dc3ecb4911cbabe4b27063c47da5fe9c",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m01-west-lane-iron-box-view-nov18": {
        "head": "c11dfc6545ec256959dacdccb6397066e624c158",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44267827043,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered West Lane view retains the trunk and pension choices and tests, reads M00 facts, and records the selected choice without treating it as approved registration.",
        "blobs": {
            "Assets/Game/Soundwell/M01WestLaneView.cs": "85e1269578162cceed2d0e5247aa8d9165ecd921",
            "Assets/Game/Soundwell/M01WestLaneView.cs.meta": "8c75a9164d712b6f13d5e8d8da8ee5af212417b3",
            "Assets/Game/Tests/Editor/M01WestLaneViewTests.cs": "3fde7cbdcd88203d6005d97045ee8364b4d5f840",
            "Assets/Game/Tests/Editor/M01WestLaneViewTests.cs.meta": "f7eba8f0254ab27131cffa12cbce110b32755331",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m00-han-ransom-count-integration-nov18": {
        "head": "0caec8cdb541a878e2dd4694672400cbc3516279",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44268626016,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered meeting records the canonical forty-one at the authored reveal and retains the PlayMode persistence and legacy-save test.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanRansomFacts.cs": "21e2c7f6121f7e01b9dbf87dbea18c88419b6a4d",
            "Assets/Game/Soundwell/M00HanRansomFacts.cs.meta": "6b1578d7d1111dd38f38ab04dd8941b5c329113c",
            "Assets/Game/Tests/Editor/M00HanRansomFactsTests.cs": "47dfa008e2a8305d3f5ccfb7559735ba9e63e593",
            "Assets/Game/Tests/Editor/M00HanRansomFactsTests.cs.meta": "940e58e4de0e4b742b52827460a2f2aa34f685c3",
            "tools/collect_unity_p0.py": "797d146c14d83f466887f37a865e9de51ac0e23f",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m00-rollcall-session-rebased-nov18": {
        "head": "43808048ef9c8c7b9bf862c89c135b005b43ea49",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44268544655,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered three-call session keeps its timing, input choices, persisted outcome and regressions, with focus and view teardown in the actual route.",
        "blobs": {
            "Assets/Game/Soundwell/M00RollCallSession.cs": "a92dc4996184bf0c510f504d2d1e2857bfce281b",
            "Assets/Game/Soundwell/M00RollCallSession.cs.meta": "53d0838dd57400f7e5ddbf2fbf8fa86197f496ce",
            "Assets/Game/Soundwell/M00RollCallView.cs": "15bf26c8193eae1c4f5b7b0ceb8f202f9a6f02f1",
            "Assets/Game/Tests/Editor/M00RollCallSessionTests.cs": "7590e1ea97ed64ad650aaa83a9770917ac32a919",
            "Assets/Game/Tests/Editor/M00RollCallSessionTests.cs.meta": "a7195a91dc3ecb4911cbabe4b27063c47da5fe9c",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/m00-hanqiao-meeting-view-integration-nov18": {
        "head": "e766b227a54d9044098d6b44a9969446db093eda",
        "verified": "867db78329f21691c7f783e1cc4446e3dbfe18c6",
        "run": 44271989923,
        "original_run": 44268924042,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered bilingual view keeps all authored meeting choices and regressions, adds confirmation at the ransom reveal, and continues to West Lane.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanQiaoMeetingView.cs": "6c405c783d9ede48539ed3ddbd755b119ac6b48f",
            "Assets/Game/Soundwell/M00HanQiaoMeetingView.cs.meta": "9cef7fe73094dd7b6c69c9d88913d4c6b4b45b0b",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingViewTests.cs": "2082f598a7fac3fdc51622dcd9a85b0f55043fa5",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingViewTests.cs.meta": "b75e350650caed9d2000a699766c7098ba129849",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "56f22374fd6598be951f3bc656a08d6645e4ed32",
            "Assets/Game/Soundwell/ReturnOpeningContinuation.cs": "f975b5f5982f7cdf169cec1e835cbdd496c9b4d3",
        },
    },
    "codex/pending/narrative-rollcall-state-machine-nov17": {
        "head": "df17d69c0cec3d896f8e1614e1c423e388849b2b",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The current six documents retain a response on any call, immediate stop, silence, distinct departure, and exact-round downstream recognition. The current outline closes the last page and stages the optional prompt before the roll call.",
        "blobs": {
            "docs/narrative/NARRATIVE_FACTS.md": "b625155cc149df2c141f0d9192b822b8bbaedc9d",
            "docs/narrative/SCENE_SCRIPT_M00.md": "55e907b358a3f6202c973f9a36c3b156c27f5c58",
            "docs/narrative/SCENE_SCRIPT_M01.md": "e8fdce5fbc0f3135fdeaddf8d6422427c9233a4d",
            "docs/narrative/SCENE_SCRIPT_M10.md": "7777ad55c57d6a6872b51713098d3e5680bea09e",
            "docs/narrative/STORY_OUTLINE.md": "2b8ac5d8e983c712d5449b5886729c6fda1d2672",
            "docs/narrative/VALIDATION_PLAN.md": "1c762e455b38d3b3423948b3ab0215397f559910",
        },
    },
    "codex/pending/v12-final-outcome-gates-nov17": {
        "head": "f534f9fedc090902118d0c1e61e2541d71f34ee0",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Merged narrative PR #101 and subsequent v1.2 revisions retain all three verified passage outcomes, independent risk consent, actual civilian exit, and epilogues gated on attendance, delivery, and the recorded result.",
        "blobs": {
            "docs/narrative/BANTER_LINES.md": "8dfe152a171a0856618c54edd275edffc871c8a4",
            "docs/narrative/CHARACTERS.md": "72ac83f42dd5b3457cdbbf2625fe2e6b1b486f81",
            "docs/narrative/NARRATIVE_FACTS.md": "b625155cc149df2c141f0d9192b822b8bbaedc9d",
            "docs/narrative/ROSTER_CASES.md": "fa527298f3f5b7e16d899ff2718c274d52012435",
            "docs/narrative/SCENE_SCRIPT_M06.md": "0b5558f2e232da5b9ce791c1f30b96400f8cbdef",
            "docs/narrative/SCENE_SCRIPT_M13.md": "6689ace4ec582616144884c4cc43cbdd983593c4",
            "docs/narrative/SCENE_SCRIPT_M14.md": "b1c3a1959e7b23593540249f38e297aa7e84a77f",
            "docs/narrative/SCENE_SCRIPT_M15.md": "b9c8defe16d037cf6a8c9d9af4a69131da39d3cb",
            "docs/narrative/SCENE_SCRIPT_M16.md": "c3f0e1163bc917bd640a66b178f0a5d752ba7eb6",
            "docs/narrative/SCENE_SCRIPT_RC04.md": "8dfac734c880dc8e98b7882d303803a8b969f463",
            "docs/narrative/SCENE_SCRIPT_RC05.md": "73e12913361ca9f3226fd2470edc36b5ee00ba21",
            "docs/narrative/STORY_OUTLINE.md": "2b8ac5d8e983c712d5449b5886729c6fda1d2672",
            "docs/narrative/VALIDATION_PLAN.md": "1c762e455b38d3b3423948b3ab0215397f559910",
        },
    },
    "codex/pending/s00-ork-bridgehead-defense-nov16": {
        "head": "c384cd855d2c2c02c196cc1647dd855818fce607",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The delivered Bootstrap wiring, live ORK encounter, attack and guard acceptance, retry and third-bell continuation supersede the failed original; current coverage also verifies localized HUD, focus, and viewport fitting.",
        "blobs": {
            "Assets/Game/Scenes/Bootstrap.unity": "28f6465ef8992724ac1ee01daf48b469d36df9a6",
            "Assets/Game/Soundwell/WhitewaterDefenseEncounter.cs": "aca3524ff1523c488d9fd2639ffb1a5d1f0df4cb",
            "Assets/Game/Soundwell/WhitewaterDefenseEncounter.cs.meta": "6049578e6b9d088f71da56dd4f4ddafa76303d10",
            "Assets/Game/Soundwell/WhitewaterPrelude.cs": "855e34e9cc91db737b46dc6bf869e5a2998b4ac3",
            "Assets/Game/Tests/Editor/WhitewaterRescueChoiceInputTests.cs": "6cdfa2f4842dddd1bd9a6cc2ed4346cbe2b3c7b8",
            "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs": "8e054d9aad211d2f655ea92277955d6f939ea0c9",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "47235b66b21e6fc05a6de31b7bde63c4b354627a",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs": "5c1f149d90b7dd20d4b00f97c6284ee049ce7cc3",
        },
    },
    "codex/pending/m00-memorial-square-opening-nov17-r2": {
        "head": "83fe90829c90342122567940d767af3b932f7b7a",
        "verified": "74fb8c6311a5452c08d9978f9cfc152c374ba56d",
        "run": 44267366431,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Merged PR #90 and its follow-ups retain the playable opening, authored responses, Makinom fact writes, GUIDs and P0 manifest, with live entry ownership and two validated gameplay tests in dev. Later responsive layout work is tracked independently.",
        "blobs": {
            "Assets/Game/Soundwell/M00MemorialSquare.cs": "5f1f37fd196f2ab11f52e90db69734b7c91a7a67",
            "Assets/Game/Soundwell/M00MemorialSquare.cs.meta": "adff095ada2a3dedbd8a1e2b901c9c200f51b083",
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "d5e77ba5e3255f4d16ab598e3bd5d19fed434ff8",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs": "93e7bcecd415820faf3e5916cbded9b416863af1",
            "Assets/Game/Tests/Editor/M00MemorialSquareTests.cs.meta": "8dbc9f43632fcac7accf1c279ce6b940627aa211",
            "tools/collect_unity_p0.py": "f9ec25265db2fd55f3ea14cdc8a0c50ecac02894",
        },
    },
    "codex/pending/m00-rollcall-fact-persistence-nov18": {
        "head": "fa00d668721077eb8d4f9e45a3689e345354aaa7",
        "verified": "30561968e1e001a467674146403919d67c00fb0b",
        "run": 44252899846,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "PR #113's runtime, GUIDs, manifest and outcomes are delivered; explicit NUnit assertions preserve all five cases.",
        "blobs": {
            "Assets/Game/Soundwell/M00RollCallFacts.cs": "ee7f8bdc1f0c1f2e2710334c27514cf9d8ee9557",
            "Assets/Game/Soundwell/M00RollCallFacts.cs.meta": "1ca99beb6a86caec085823e94743404407dc43b1",
            "Assets/Game/Tests/Editor/M00RollCallFactsTests.cs": "bf9a9054cccc0b123b6d4797034827c257ceacc1",
            "Assets/Game/Tests/Editor/M00RollCallFactsTests.cs.meta": "8fc82b68176028202138a6782dd235f8e6933ba2",
            "tools/collect_unity_p0.py": "f9ec25265db2fd55f3ea14cdc8a0c50ecac02894",
        },
    },
    "codex/pending/m00-hanqiao-meeting-dialogue-model-nov18": {
        "head": "19196a9bcc83ad404e42cf80b4cfd5eb94673c3f",
        "verified": "8359de7ce5b54c4fbbce22e73754dd8e0a26e048",
        "run": 44255501304,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "PR #114's bilingual choices and branches are delivered; explicit cases and ledger wording follow the authored M00 script.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanQiaoMeeting.cs": "bc698e23ee7c64ecb3fbfdf63ef5c40e5ee4c737",
            "Assets/Game/Soundwell/M00HanQiaoMeeting.cs.meta": "dad6f436b7de77f8c36f013aaae8f861c381c677",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingTests.cs": "f09c88cfc8f52b8f63bfadf8516006e16b316013",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingTests.cs.meta": "29a2492ee8c36d63df941813aff735e141b3b73b",
        },
    },
    "codex/pending/m00-hanqiao-meeting-dialogue-model-nov18-r2": {
        "head": "2849136aa1018a5720df4e3049fc483a99b05dd6",
        "verified": "8359de7ce5b54c4fbbce22e73754dd8e0a26e048",
        "run": 44255501304,
        "original_run": 44246678633,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "The seven named meeting tests are retained with an added ledger-copy assertion and matching authored wording.",
        "blobs": {
            "Assets/Game/Soundwell/M00HanQiaoMeeting.cs": "bc698e23ee7c64ecb3fbfdf63ef5c40e5ee4c737",
            "Assets/Game/Soundwell/M00HanQiaoMeeting.cs.meta": "dad6f436b7de77f8c36f013aaae8f861c381c677",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingTests.cs": "f09c88cfc8f52b8f63bfadf8516006e16b316013",
            "Assets/Game/Tests/Editor/M00HanQiaoMeetingTests.cs.meta": "29a2492ee8c36d63df941813aff735e141b3b73b",
        },
    },
    "codex/pending/r2-04-configurable-actor-battle-bounds-nov16": {
        "head": "b637f21f1e083777d0995725eb2ab0860c3de220",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Serialized arena extents, footprint assertions, and per-room camera framing are delivered with finite-value, rotation and viewport coverage.",
        "blobs": {
            "Assets/Game/Soundwell/PartyMovement2D.cs": "06f0a6d40fd366ee65144ee0ad7225ad5ade8014",
            "Assets/Game/Soundwell/SoundwellCameraRig.cs": "2e0882e6a2787c500bfbf2a35c405c136b628763",
            "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs": "8e054d9aad211d2f655ea92277955d6f939ea0c9",
            "Assets/Game/Tests/Editor/SoundwellTrialTests.cs": "caa05c53251bf59221b6d7fde286c2530d72eb13",
        },
    },
    "codex/pending/r2-05-event-driven-entry-binding-refresh-nov16": {
        "head": "0cf41b24145c8e58cb9f0f55a918b974abe96065",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Entry action-change filtering, bounded outcome polling and the effective-binding regression are already present.",
        "blobs": {
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "d5e77ba5e3255f4d16ab598e3bd5d19fed434ff8",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs": "285ebf1303162baf24074f093a8526f14c3c6f0e",
        },
    },
    "codex/pending/r2-10-localized-hud-adaptive-capture-nov16": {
        "head": "5bfe6163c5221187dd4125883b0864d0ed08905e",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "original_run": 43795934140,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Localized glyph sampling uses the live HUD title rect and retains the clipping regression in current dev.",
        "blobs": {
            "Assets/Game/Soundwell/SoundwellSmokeHost.cs": "d10dd2259f872db214032770edfb6ea2610313f9",
            "Assets/Game/Tests/Editor/SoundwellLocalizedRescueCopyTests.cs": "47235b66b21e6fc05a6de31b7bde63c4b354627a",
        },
    },
    "codex/pending/r2r3-recheck-after-s00-arrival-fix-nov16": {
        "head": "bcb297615f46f2412809eee378e9f84ea15b7318",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "original_run": 43797019308,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Atlas validation, beast-wall sweep, configurable arena footprint and entry binding observer all have current dev implementations and regressions.",
        "blobs": {
            "Assets/Game/Editor/SoundwellSpriteAtlasSetup.cs": "e288fdd91d8abb44f63306fa33167973e1bf5bd0",
            "Assets/Game/Soundwell/BeastPatrolAndChase.cs": "61e31dab8fe493e485051308f16f220032fbc29a",
            "Assets/Game/Soundwell/PartyMovement2D.cs": "06f0a6d40fd366ee65144ee0ad7225ad5ade8014",
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "d5e77ba5e3255f4d16ab598e3bd5d19fed434ff8",
            "Assets/Game/Tests/Editor/BeastPatrolCorridorTests.cs": "7246ba6d6fea9fb3f9ade97cc355bcab64586c8d",
            "Assets/Game/Tests/Editor/P0BootstrapTests.cs": "a3f8d8ea6e3c2036684f090df552d713c8249db1",
            "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs": "8e054d9aad211d2f655ea92277955d6f939ea0c9",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs": "285ebf1303162baf24074f093a8526f14c3c6f0e",
        },
    },
    "codex/pending/r3-tilemap-vendor-integrated-after-s00-nov16": {
        "head": "03a53d2f411ef580671057ffdfb969877b35b482",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "original_run": 43807472584,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Read-only loaded Tilemap validation, bounded ORK spools, empty folder metas and installer tests are delivered.",
        "blobs": {
            "Assets/Game/Editor/SoundwellTilemapSetup.cs": "e8ccc07d452c45b2d6c30392b066234c4cae5018",
            "Assets/Game/Tests/Editor/P0BootstrapTests.cs": "a3f8d8ea6e3c2036684f090df552d713c8249db1",
            "tools/install_ork.py": "732e885989f3083b83605c4c3f817717a10ba31b",
            "tools/test_collect_unity_p0.py": "657c894e49102ade91ef3c2a2868356fd6e248c4",
        },
    },
    "codex/pending/soundwell-formation-toggle-gamepad-regression-nov16": {
        "head": "7072009be806c168bb8c5bc2176995dbf056e410",
        "verified": "1f5b551aa251a604dd80f2be61fae15eafa252a2",
        "run": 44265999587,
        "original_run": 44032133709,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "Actual Gamepad Submit changes the entry formation preference without entering the trial; focus restoration and manifest are delivered.",
        "blobs": {
            "Assets/Game/Soundwell/SoundwellEntrance.cs": "d5e77ba5e3255f4d16ab598e3bd5d19fed434ff8",
            "Assets/Game/Soundwell/SoundwellInputAdapter.cs": "09b3a427f7f64a577383716e48b607908c3e9b8b",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs": "5c1f149d90b7dd20d4b00f97c6284ee049ce7cc3",
            "tools/collect_unity_p0.py": "f9ec25265db2fd55f3ea14cdc8a0c50ecac02894",
        },
    },
    "codex/pending/story-003-ork-attack-choice": {
        "head": "6f546b89d4a2bc59688e82bb2451adf13c24290a",
        "verified": "84f7ff5a255b56c1099daa7421f7e22e8546ed50",
        "run": 43757378497,
        "original_run": 43125796222,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "old attack-choice and diagnostic prototype replaced by live ORK base-attack setup, actual UGUI action/target, natural victory, and retained inspection tool",
        "blobs": {
            "Assets/Game/Soundwell/OrkPartyBridge.cs":
                "4a83d6deafead6919ad1ecacd88319e9c1854a73",
            "Assets/Game/Soundwell/TrialFlowController.Runtime.cs":
                "2f599eb1969c92197a53fc08e5d1acacb93b64af",
            "Assets/Game/Soundwell/SoundwellView.cs":
                "d003df80b4f4877779e9790f8314a4d3bb571ba7",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "24f5549d5875edf728dfb8b1430ea6d46aa9b38c",
            "tools/inspect_ork_battle_source.py":
                "6557a792eb71736b2f2f5234d009012d56c7c163",
            "tools/collect_unity_p0.py":
                "7e871c4fdd5d71841fcc4ec5f0d1d7c60457eb00",
        },
    },
    "codex/pending/s00-escort-separation-visual-regroup-nov13": {
        "head": "e3cfeb109d8dece1db29ee400c055100856f5894",
        "verified": "84f7ff5a255b56c1099daa7421f7e22e8546ed50",
        "run": 43757378497,
        "original_run": 43671553145,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "green/amber stop and off-lane recovery retained with stricter bilingual visual/readable state and live input test",
        "blobs": {
            "Assets/Game/Soundwell/WhitewaterPrelude.cs":
                "6c9a6282c4cb6e0feab8bec4b9002785972537ef",
            "Assets/Game/Tests/Editor/WhitewaterRescueChoiceInputTests.cs":
                "bc2e280a857fa83ed68d21e212bea246bde68a55",
            "tools/collect_unity_p0.py":
                "7e871c4fdd5d71841fcc4ec5f0d1d7c60457eb00",
        },
    },
    "codex/pending/story003-escort-checkpoint-follower-resync-nov14": {
        "head": "878e2dbd8976dd840c9855229bda984e92ba5f57",
        "verified": "84f7ff5a255b56c1099daa7421f7e22e8546ed50",
        "run": 43757378497,
        "original_run": 43619755853,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "checkpoint recovery signal, ORK-owned formation reposition, Rigidbody2D leader sampling and two-frame regression remain in the verified current code",
        "blobs": {
            "Assets/Game/Soundwell/OrkPartyBridge.cs":
                "4a83d6deafead6919ad1ecacd88319e9c1854a73",
            "Assets/Game/Soundwell/PartyFormationFollower.cs":
                "37631c5049b75a648e1397f52013b56701b19f4f",
            "Assets/Game/Soundwell/TrialFlowController.Scene.cs":
                "189ddf91570bbffb39a65b30e3b535c17f260f21",
            "Assets/Game/Tests/Editor/SoundwellTrialTests.cs":
                "3a0e8f2edc79232eb3256406a561120ca370b056",
        },
    },
    "codex/pending/story003-fixed-full-rescue-evidence-nov14": {
        "head": "4ca0a38d83a9de80b7b1bd9eaf6e32796eeb9834",
        "verified": "84f7ff5a255b56c1099daa7421f7e22e8546ed50",
        "run": 43757378497,
        "original_run": 43590517380,
        "ancestor": True,
        "pin_verified_snapshot": True,
        "reason": "fixed-formation win still finishes the physical NPC escort and one ORK quest reward under exact registered UnityTest and collector",
        "blobs": {
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "24f5549d5875edf728dfb8b1430ea6d46aa9b38c",
            "tools/collect_unity_p0.py":
                "7e871c4fdd5d71841fcc4ec5f0d1d7c60457eb00",
        },
    },
    "codex/pending/story-003-battle-victory-v2": {
        "head":"dcd7351b3461c78aa8957c2c9c19cd017c50ebd2",
        "verified":"95d90e3548a599d388def4b6ce72d06f32a8cde8",
        "run":43111500532,
        "ancestor":True,
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "b36d48efdf6616bf5dc1f0c7f4ab0bdfd70f0af7",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "e75ec9dac66c9766e543f6c88f63d07817a11564",
        },
    },
    "codex/pending/ci-exact-sha-evidence-reuse-recovered-nov11": {
        "head":"f503c588fcd58229020bb7b6e1aadd8d37366893",
        "verified":"f503c588fcd58229020bb7b6e1aadd8d37366893",
        "run":43112019720,
        "ancestor":False,
        "blobs":{
            ".github/workflows/unity-p0.yml":
                "7784f58fdd3d62c58cbdd63fc2045ff9b9276433",
            "tools/reuse_unity_evidence.py":
                "e347f976fbaa7b3a2d362dadb5026d458b814039",
            "tools/test_reuse_unity_evidence.py":
                "e62b29221efe1682b2940f861016a2707b8a98eb",
        },
    },
# Fully reviewed one-off retirements: each original HEAD had green
# Unity CI; all candidate patch additions are present in the verified
# dev replacement, or (M5) tightened in that replacement. Blobs
# pin the complete replacement source/test surface against drift.
    "codex/pending/m2-rebinding-invalid-preference-recovery-nov11": {
        "head":"3c369a1936b55e6790fff3f7b2f900b55b609ad7",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"full M2 corrupt/valid override handling and NUnit coverage reproduced in dev",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellInputAdapter.cs":
                "ae0166baac0de887acbe028e3f7a25991d7120c3",
            "Assets/Game/Tests/Editor/P0BootstrapTests.cs":
                "061f2ac2011f1da289bd4a6d54588f6d268dd8ce",
            "tools/collect_unity_p0.py":
                "3fbd2169d1c1e4c74ae8fd99fc252ba1da0da8a1",
            "tools/test_collect_unity_p0.py":
                "297b81a0c2f7523e680a53efc7a990e0aa7dbb28",
        },
    },
    "codex/pending/m5-exact-wall-audit-on-current-dev-nov12": {
        "head":"fed94a532ceb6e88367619429ab5235333f0dc60",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"cell footprint and solidity assertions preserved and strengthened by current M5 validator",
        "blobs":{
            "Assets/Game/Editor/SoundwellTilemapSetup.cs":
                "8a8e547ebf9b75ce6162601e7765479566bfd193",
        },
    },
    "codex/pending/story003-companion-battle-step-pixel-pose-nov12": {
        "head":"79a3ae892fd6813089356f0ff8a0d9ae1ad88650",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"all complete step pose patch additions preserved in current game and tests",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "4cab8c26568cb0f426307d272699ffeceea89811",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "644b493d5bd1239ceb80dd913123d3c3b0f7b550",
        },
    },
    "codex/pending/story003-companion-step-after-escort-nov12": {
        "head":"12e6e047c79fe61f7e945a4cdfd52797fa340f5f",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"all committed post-escort step pose lines preserved in current game and tests",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "4cab8c26568cb0f426307d272699ffeceea89811",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "644b493d5bd1239ceb80dd913123d3c3b0f7b550",
        },
    },
    "codex/pending/story-003-ork-attack-visual-after-focus-nov11": {
        "head":"4a6f9df67700abfd06eddc9dc879a4ae00742c0e",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"full ORK attack visual feedback and exact test registration preserved",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellTrial.cs":
                "c47416e6253b3398a70098bce45ab880c6b2269d",
            "Assets/Game/Tests/Editor/SoundwellTrialTests.cs":
                "0539f8f8048f623a4d80547b54860ed2dce5bc90",
            "tools/collect_unity_p0.py":
                "3fbd2169d1c1e4c74ae8fd99fc252ba1da0da8a1",
            "tools/test_collect_unity_p0.py":
                "297b81a0c2f7523e680a53efc7a990e0aa7dbb28",
        },
    },
    "codex/pending/story-003-ork-attack-visual-feedback-nov11": {
        "head":"0f0043d93b8ce664fe747e317bfad0c3c8ef9a3e",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "reason":"full ORK attack visual feedback and exact test registration preserved",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellTrial.cs":
                "c47416e6253b3398a70098bce45ab880c6b2269d",
            "Assets/Game/Tests/Editor/SoundwellTrialTests.cs":
                "0539f8f8048f623a4d80547b54860ed2dce5bc90",
            "tools/collect_unity_p0.py":
                "3fbd2169d1c1e4c74ae8fd99fc252ba1da0da8a1",
            "tools/test_collect_unity_p0.py":
                "297b81a0c2f7523e680a53efc7a990e0aa7dbb28",
        },
    },
# One-off reviewed candidate retirements. Source/test snapshots
# are pinned at the validated dev SHA, not guessed from branch names.
    "codex/pending/s00-last-boat-hanqiao-rescue-nov13": {
        "head":"7fa2b136c53523dacb595bd1d36600697a475f2d",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"S00 Han Qiao last-boat beats and regression tests preserved; copy revised in verified dev",
        "blobs":{
            "Assets/Game/Soundwell/WhitewaterPrelude.cs":
                "3a0356eaceef53f667221deb73f5c15cf91cc3ed",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs":
                "c0fcb044887bd160b8bc9d338ce7552eadc0a9a4",
        },
    },
    "codex/pending/s00-workshop-world-back-integrated-nov13": {
        "head":"4293d51eec7e4783e18935b1f537b129026c87bf",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"S00 workshop promise/world-back transition and tests retained; choice layout evolved",
        "blobs":{
            "Assets/Game/Soundwell/WhitewaterPrelude.cs":
                "3a0356eaceef53f667221deb73f5c15cf91cc3ed",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs":
                "c0fcb044887bd160b8bc9d338ce7552eadc0a9a4",
        },
    },
    "codex/pending/s00-workshop-promise-choice-nov13": {
        "head":"59337e8db63b3adca79281fb5079c4a76eee571d",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"S00 workshop choice and existing tests retained; button placement evolved",
        "blobs":{
            "Assets/Game/Soundwell/WhitewaterPrelude.cs":
                "3a0356eaceef53f667221deb73f5c15cf91cc3ed",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs":
                "c0fcb044887bd160b8bc9d338ce7552eadc0a9a4",
        },
    },
    "codex/pending/s00-world-back-returns-focus-nov13": {
        "head":"109ac11bb876c77098b3102bce7ef1a127be9c53",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"all world-back focus fix and regression additions retained",
        "blobs":{
            "Assets/Game/Soundwell/WhitewaterPrelude.cs":
                "3a0356eaceef53f667221deb73f5c15cf91cc3ed",
            "Assets/Game/Tests/Editor/SoundwellInputSystemTests.cs":
                "c0fcb044887bd160b8bc9d338ce7552eadc0a9a4",
        },
    },
    "codex/pending/story-003-companion-attack-pixel-pose-nov11": {
        "head":"c1a4826e0b86e38a1e8cec2262b913575c6bd7f2",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"pixel attack presentation and companion tests preserved with updated role/controller and assertions",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "4cab8c26568cb0f426307d272699ffeceea89811",
            "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs":
                "c593e3e0caa491e14de9b675eb029c81e752ac17",
            "Assets/Game/Tests/Editor/SoundwellCompanionActionTests.cs.meta":
                "bdd895ef774607ac303b1b209d46989ecaa0b680",
            "tools/collect_unity_p0.py":
                "3fbd2169d1c1e4c74ae8fd99fc252ba1da0da8a1",
            "tools/test_collect_unity_p0.py":
                "297b81a0c2f7523e680a53efc7a990e0aa7dbb28",
        },
    },
    "codex/pending/story-003-visible-ork-guard-pose-nov12": {
        "head":"afa7ceb0f49f603caf3554dba47a69cfbf137543",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"legacy failing guard-event prototype superseded by accepted ORK guard shield presentation and runtime test",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "4cab8c26568cb0f426307d272699ffeceea89811",
            "Assets/Game/Tests/Editor/SoundwellOrkBattleTests.cs":
                "644b493d5bd1239ceb80dd913123d3c3b0f7b550",
        },
    },
    "codex/pending/story003-ork-step-resolution-trace-nov12": {
        "head":"68342dc638073c00e129b6ec018de86114c21b08",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"throwaway ORK step debug instrumentation retired; its actual step assertions retained",
        "blobs":{
            "Assets/Game/Soundwell/SoundwellOrkParty.cs":
                "4cab8c26568cb0f426307d272699ffeceea89811",
            "Assets/Game/Tests/Editor/SoundwellThreatPreviewTests.cs":
                "9d307e1dee145ea75047ea303e8af0d1a391252b",
        },
    },
    "codex/pending/ci-retire-reviewed-legacy-candidates-nov13": {
        "head":"f9cbbb02501d8180c39373f2ec3a9f9eda8667ca",
        "workflow_only":True,
        "run":43523760012,
        "reason":"merged cleanup-only commit passed its exact-SHA branch pruning run",
    },
    # Verified replacement keeps the three *different* ORK
    # actions (latch/gate, accepted Guard, and target attack),
    # physical rescue/escort, HUD chooser focus, localized labels,
    # and the full live battle tests; legacy button plumbing was
    # intentionally replaced by that newer verified implementation.
    "codex/pending/story-003-guard-gate-turn-roles": {
        "head":"4f3c15e0bf7253ae2423a00b3c6a8eaf3757252f",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"real ORK gate/guard/attack roles and gamepad/UGUI chooser validated in replacement",
        "blobs":LEGACY_ROLE_BLOBS,
    },
    "codex/pending/story-003-rescue-turn-and-route-validation": {
        "head":"6d7c0a8373ad1a4e79b24cd5077fcf5578c079a2",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"failed old route assertion superseded by live physical gate, combat, escort and reward tests",
        "blobs":LEGACY_ROLE_BLOBS,
    },
    "codex/pending/story-003-turn-roles-after-m6": {
        "head":"74cede75da5f852b5de94f8f30c11d861f968821",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"early turn role buttons replaced by tested ORK action choices and physical gate reach",
        "blobs":LEGACY_ROLE_BLOBS,
    },
    "codex/pending/story-003-verified-rescue-roles": {
        "head":"123b99d7a97590250fe08fd7169fdeea830cf6ae",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"old failing rescue-role test superseded by successful live gate, guard, attack and escort",
        "blobs":LEGACY_ROLE_BLOBS,
    },
    "codex/pending/story003-guard-m4-m10-integrated-nov12": {
        "head":"0522622b6ad901dec69a31b357795e9ffb7d2bb6",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"guard sprite/animation and memory probe retained; old ORK guard event test replaced by accepted shield turn",
        "blobs":LEGACY_GUARD_M4_M10_BLOBS,
    },
    "codex/pending/story003-surveyor-world-dialogue-zh-nov12": {
        "head":"47e01f041523789bef6f8d05857224876003dab6",
        "verified":"5f0ea77e27be49d8f793351524624ea101e52483",
        "run":43518659065,
        "ancestor":True,
        "pin_verified_snapshot":True,
        "reason":"Chinese NPC/sign/cart/rope localization now uses the verified route_posted/kit_reclaimed/cart_mentioned/sign_removed and observed.* replacements; smoke still verifies physical map-kit recovery and return dialogue",
        "blobs":{
            "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-Shared.asset":
                "7a7441a0981858308e9f610918da8670ace8dab5",
            "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-en.asset":
                "05101562a41e5795ef0ce855e361e2e49ff129d6",
            "Assets/Game/Resources/Soundwell/Localization/SoundwellUI-zh-Hans.asset":
                "93d53e856e2f73ea6d071b6ad9f582fd78b221b3",
            "Assets/Game/Soundwell/SoundwellSmokeHost.cs":
                "883edbee175922067b56bf4375d403200df2cf9c",
            "Assets/Game/Soundwell/SoundwellView.cs":
                "1f4ebd3db1f26e4eaca7d54230d5297e5cedc424",
        },
    },
}
