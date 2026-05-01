# Changelog

이 프로젝트의 모든 변경 사항은 이 파일에 기록됩니다.

포맷은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/) 를 따르며,
버전 체계는 [Semantic Versioning](https://semver.org/lang/ko/) 을 따릅니다.

## [1.0.0] - 2026-05-01

### Added
- 초기 UPM 패키지 릴리즈
- `Tools > particle auto sorting` 에디터 윈도우
- 프리팹 분석: `Above`/`Below` 자식의 ParticleSystemRenderer + TrailRenderer + Trail Module 수집
- Material 런(run) 단위 OiL/Fudge 자동 할당
  - Above = `charSortingOrder + 1, +2, …, +N`
  - Below = `charSortingOrder - N, …, -1`
  - Fudge: `(size - 1 - rank) * step`, default step=30, 사용자 지정 가능 (1~10000)
- 배치 수 계산 (BatchKey 기반)
- 인터리브 감지 및 경고 pill
- 오브젝트 단위 불변식 보장 (OrderInLayer 단조성, Fudge 단조성)
- 오버플로 감지 시 적용 차단 (short 범위 초과)
- 수정 모드: Hierarchy Sibling 드래그앤드롭 재정렬 + 미리보기
- Undo 그룹 기반 일괄 적용 (Ctrl+Z 한 번에 전체 롤백)
- CSV 리포트 출력 (UTF-8 BOM, 프리팹 내 누적 배치 수 컬럼 포함)
- "하이어라키만 재정렬" 별도 적용 버튼
- 외부 패키지 의존 0개 (asmdef 단일, Editor 플랫폼만)
