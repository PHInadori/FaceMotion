# FaceMotion Documentation

FaceMotion の基本文書は日本語中心です。UI の technical identifier、package ID、diagnostic code は英語表記を維持します。

## User Documentation

- [Getting Started](Getting-Started.md) - 最短の作成から VRChat Build & Test まで
- [Installation](Installation.md) - 対応環境、VCC / ALCOM、手動導入
- [Uninstall and Update](Uninstall-Update.md) - package removal、reinstall、schema update の実測済み挙動
- [User Guide](User-Guide.md) - project、track、timeline、preview、preset、Undo
- [Export](Export.md) - AnimationClip export の制約と overwrite
- [VRChat Integration](VRChat-Integration.md) - Direct Integration と安全性
- [Modular Avatar](Modular-Avatar.md) - 任意 MA backend の workflow
- [Diagnostics](Diagnostics.md) - 診断コード、action level、選択ボタンの安全規則
- [Troubleshooting](Troubleshooting.md) - よくある問題と diagnostic category
- [Compatibility](Compatibility.md) - 保存データと互換性の利用者向け contract

## Developer Documentation

- [Schema Migration](Schema-Migration.md) - migration implementation contract
- [CI](CI.md) - automated validation と必要 secrets
- [VPM Distribution](VPM-Distribution.md) - release artifact と listing repository の運用
- [Architecture](Architecture/README.md) - assembly / serialization / ADR index
- [Testing](Testing/) - manual and boundary checklists
- [Performance and Stress Audit](Performance.md) - Phase I.6 の manual benchmark と optimization guardrails
- [Release Checklist](Release-Checklist.md) - tag、release、post-release の手順
- `Phase-*.md` と `Architecture/Phase-*-Implementation-Inventory.md` - historical implementation records

0.x の間、FaceMotion は external public API の安定性を保証しません。serialized data の compatibility contract は [Compatibility](Compatibility.md) を参照してください。
