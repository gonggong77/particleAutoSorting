# Particle Auto Sorting — 사용 가이드

> 메뉴: **Tools > particle auto sorting**
> 진입 클래스: `ParticleAutoSorting.Editor.ParticleAutoSortingWindow`
> 대상 사용자: Unity 6000.0+ 에서 파티클 시스템이 많은 프리팹의 렌더링 순서·배치 수를 한 번에 정리하고 싶은 VFX/Tech Artist

---

## 목차

1. [시작하기](#1-시작하기)
2. [프리팹 구조 가정](#2-프리팹-구조-가정)
3. [UI 한눈에 보기](#3-ui-한눈에-보기)
4. [기본 워크플로 (5단계)](#4-기본-워크플로-5단계)
5. [수정 모드 — Hierarchy Sibling 재정렬](#5-수정-모드--hierarchy-sibling-재정렬)
6. [경고 종류와 대응](#6-경고-종류와-대응)
7. [Char Sorting Order / Fudge 간격](#7-char-sorting-order--fudge-간격)
8. [CSV 리포트](#8-csv-리포트)
9. [도메인 규칙 (참조)](#9-도메인-규칙-참조)
10. [트러블슈팅 / FAQ](#10-트러블슈팅--faq)

---

## 1. 시작하기

### 설치
README의 [설치 방법](../README.md#설치-방법) 참조. UPM Git URL 한 줄.

### 첫 실행
1. Unity 메뉴 **Tools > particle auto sorting** 클릭
2. 빈 윈도우가 열리면 좌측 **DropZone**에 분석할 프리팹들을 드래그앤드롭
3. 자동으로 분석되어 프리팹 리스트가 채워짐

도구는 **Editor 전용**입니다. Runtime 빌드에는 포함되지 않습니다.

---

## 2. 프리팹 구조 가정

이 도구는 다음 구조의 프리팹을 전제로 합니다:

```
PrefabRoot
├── Above              ← char(캐릭터) 위에 그려질 파티클들의 부모
│   ├── flash01        ← ParticleSystem / TrailRenderer 한 개 이상
│   ├── flash02
│   └── flash03
└── Below              ← char 아래에 그려질 파티클들의 부모
    ├── dust01
    ├── dust02
    └── dust03
```

- 작업자가 의도적으로 만들어둔 `Above` / `Below` 자식 구조가 있어야 합니다.
- Above/Below 각각의 자식들은 **Hierarchy 하단일수록 시각적으로 앞**에 그려져야 한다는 규칙을 따릅니다.
- `Above`/`Below` 가 없는 프리팹은 분석되지 않거나 빈 결과가 나옵니다.

### 같은 GameObject에 Particle + Trail 공존 시
- Trail은 Particle 위(앞)에 그려지도록 자동 보정 (`HierarchyOrder = Particle + 0.5`)

---

## 3. UI 한눈에 보기

```
┌─────────────────────────────────────────────────────────────────┐
│  TopBar:  [수정 모드 OFF]  Char Sorting Order: 0  Fudge간격: 30  │  ← 전역 컨트롤
├─────────────────────────────────────────────────────────────────┤
│ ┌──────────────┐ │  Above 테이블                                 │
│ │  DropZone    │ │  ┌────────────────────────────────────────┐  │
│ │  프리팹 리스트│ │  │ ≡  PS Renderer  Material  OiL  Fudge  │  │
│ │  - prefabA   │ │  │ ≡  Trail        Material  OiL  Fudge  │  │
│ │  - prefabB ⚠ │ │  └────────────────────────────────────────┘  │
│ │  - prefabC   │ │  Below 테이블                                  │
│ │              │ │  ┌────────────────────────────────────────┐  │
│ │              │ │  │ ≡  ...                                 │  │
│ └──────────────┘ │  └────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│  [재계산] [미리보기] [Undo] [적용] [하이어라키만 재정렬]  + CSV  │  ← 하단 버튼
├─────────────────────────────────────────────────────────────────┤
│  StatusBar:  3개 프리팹 / 배치 수 24→12 / 마지막 적용: 17:42     │
└─────────────────────────────────────────────────────────────────┘
```

### 주요 영역
| 영역 | 역할 |
|---|---|
| **TopBar** | 수정 모드 토글, Char Sorting Order, Fudge 간격 입력 |
| **DropZone / 프리팹 리스트** | 프리팹 추가, 경고 pill 표시, 행 클릭 = 활성 프리팹 |
| **Above / Below 테이블** | 활성 프리팹의 Renderer 단위 OiL/Fudge 표시 + 드래그 핸들 |
| **하단 버튼** | 재계산·미리보기·Undo·적용 / CSV 패널 |
| **StatusBar** | 전체 통계, 마지막 작업 시각 |

---

## 4. 기본 워크플로 (5단계)

### Step 1 — 프리팹 추가
- DropZone에 Hierarchy/Project에서 프리팹 드래그앤드롭
- 여러 개를 한 번에 끌어다 놓아도 됨
- 같은 프리팹은 중복 추가되지 않음

### Step 2 — 분석 결과 확인
- 좌측 리스트에 프리팹 이름과 (있다면) 경고 pill 표시
- 행을 클릭하면 우측 테이블에 Above/Below Renderer 목록 노출
- 각 행은 **Hierarchy 위→아래** 순서로 정렬됨

### Step 3 — Char Sorting Order 설정
- TopBar의 **Char Sorting Order** 입력
- char(캐릭터)의 SpriteRenderer/SkinnedMeshRenderer가 가진 Order in Layer 값을 입력
- 이 값을 기준으로 Above는 +1, +2, ... / Below는 -1, -2, ... 가 자동 할당됨

### Step 4 — 미리보기
- 하단 **[재계산]** 클릭 → 메모리상 OiL/Fudge 미리보기
- 디스크에는 아직 변경 없음 (Ctrl+Z 영향 X)
- 테이블의 OiL / Fudge 컬럼이 실시간으로 갱신됨
- 인터리브, 오버플로 등의 경고가 이 시점에 노출됨

### Step 5 — 적용
- 하단 **[적용]** 클릭
- 모든 프리팹이 디스크에 일괄 저장 (`PrefabUtility.SavePrefabAsset`)
- **하나의 Undo 그룹**으로 묶여 Ctrl+Z 한 번으로 전체 롤백
- 오버플로가 있는 프리팹이 하나라도 있으면 적용이 차단됨

---

## 5. 수정 모드 — Hierarchy Sibling 재정렬

인터리브를 줄이거나 의도한 그림 순서를 바꾸기 위해 **같은 부모 안에서 Sibling 순서**를 바꿔야 할 때 사용합니다.

### 켜는 방법
TopBar의 **수정 모드** 토글을 ON.

### 변경되는 동작
| 항목 | OFF (기본) | ON (수정 모드) |
|---|---|---|
| 행 좌측 `≡` 핸들 | 비활성 | 드래그 가능 |
| `[재계산]` 버튼 | 라벨 그대로 | **`[미리보기]`** 로 토글 |
| Fudge 간격 입력 | 비활성 | **활성** (1~10000, 변경 시 자동 RecomputeAll) |

### 드래그 규칙
- `≡` 핸들을 잡고 같은 부모 안의 다른 위치로 끌어다 놓을 수 있음
- 같은 transform의 모든 Renderer (PS + Trail)가 함께 이동
- **다른 부모로의 이동은 차단** (Above ↔ Below 이동, 다른 그룹 이동 모두 불가)
- 메모리상 `HierarchyOrder` 가 즉시 갱신되어 테이블이 재정렬됨

### 적용 순서
1. 수정 모드 ON
2. `≡` 드래그로 Sibling 재정렬 (메모리만 변경)
3. `[미리보기]` 클릭 → 새 OiL/Fudge 계산
4. 결과 확인
5. `[적용]` 클릭 → 디스크 저장 시 `SetSiblingIndex` + `SavePrefabAsset` 일괄 수행

### "↻ 미리보기 필요" 뱃지
재정렬 후 `[미리보기]` 를 클릭하지 않은 행에는 이 뱃지가 표시됩니다. 미리보기 없이 적용하면 안 됩니다.

### "하이어라키만 재정렬" 버튼
Sibling 순서만 디스크에 반영하고 **OiL/Fudge 값은 기존 그대로 유지**해야 할 때 사용. (예: 이미 다른 도구로 OiL/Fudge를 세팅해 둔 프리팹의 Hierarchy 순서만 정리)

---

## 6. 경고 종류와 대응

### ⚠ 인터리브 경고 (`HasInterleaveWarning`)
- **의미**: 동일 Material이 여러 런(run)으로 쪼개졌다 = Hierarchy 상에서 다른 Material이 중간에 끼어들어 있음
- **영향**: 불변식은 보장되지만 **배치 수가 최적이 아님**
- **대응**: 수정 모드 ON → 같은 Material끼리 인접하도록 Sibling 드래그 → 재계산
- 표시 위치: 프리팹 리스트의 행 + 해당 Renderer 행 좌측 pill

### ⚠ 오버플로 (`HasOverflow`)
- **의미**: OiL 또는 Fudge 값이 Unity short 범위(-32768~32767)를 벗어남
- **영향**: **적용 차단**. clamp 등의 임의 보정은 절대 하지 않음
- **대응**:
  - Char Sorting Order 값을 줄이거나
  - Above/Below의 Material 런 개수를 줄이거나 (Sibling 정리)
  - Fudge 간격(step)을 더 작은 값으로 조정

### ⚠ 비활성 오브젝트 경고
- **의미**: Particle/Trail 오브젝트 또는 부모 GameObject가 SetActive(false)
- **영향**: 배치 수 카운트에서 제외, 경고 pill 표시
- **대응**: 의도한 대로면 무시. 실수면 활성화

### null Material
- **silent skip**: 경고 없이 무시됨 (Spec 규칙)

---

## 7. Char Sorting Order / Fudge 간격

### Char Sorting Order
char(캐릭터)의 Order in Layer 값. 이 값을 기준으로:
- Above 런들 = `charSortingOrder + 1, +2, …, +N` (Hierarchy 위→아래 순)
- Below 런들 = `charSortingOrder - N, …, -1`
- N = 해당 bucket의 Material 런 개수

예) charSortingOrder = 100, Above 런 3개, Below 런 2개 →
- Above OiL: 101, 102, 103
- Below OiL: 98, 99

### Fudge 간격 (step)
동일 런 안에서 Renderer마다 부여될 Fudge 값 간격.

```
fudge[rank] = (size - 1 - rank) * step
```

- `rank 0` = 런 최상단 (Hierarchy 가장 위) = 시각적으로 뒤 = 최대 Fudge
- `rank size-1` = 런 최하단 = 시각적으로 앞 = Fudge 0
- step default = 30
- 수정 모드 ON 시에만 사용자 변경 가능 (1~10000 클램프, 변경 즉시 RecomputeAll)

예) step=30, size=4 → fudge = [90, 60, 30, 0]

---

## 8. CSV 리포트

### 내보내기
하단 CSV 패널에서 **[CSV 저장]** 클릭 → 저장 위치 지정 → UTF-8 BOM CSV 파일 생성. Excel/Google Sheets에서 한글 깨짐 없이 열림.

### 컬럼 (요약)
| # | 컬럼 | 설명 |
|---|---|---|
| 1 | 프리팹 경로 | Project 내 .prefab 경로 |
| 2 | 그룹 | `Above` / `Below` |
| 3 | Hierarchy Order | 그룹 내 위→아래 순서 |
| 4 | GameObject 이름 | Renderer가 붙은 transform 이름 |
| 5 | Renderer 종류 | `Particle` / `Trail` / `TrailModule` |
| 6 | Material | sharedMaterial 이름 |
| 7 | OrderInLayer (Before/After) | 적용 전후 OiL |
| 8 | SortingFudge (Before/After) | 적용 전후 Fudge |
| 9 | 런 인덱스 | 그룹 내 Material 런 번호 |
| 10 | **프리팹 내 누적 배치 수** | Frame Debugger 스타일 누적 |
| 11 | 프리팹 총 배치 수 (Before) | |
| 12 | 프리팹 총 배치 수 (After) | |
| 13 | 경고 | 인터리브, 오버플로, 비활성 등 |

### "프리팹 내 누적 배치 수" 컬럼이란
Frame Debugger와 동일한 카운팅 방식:
1. Above/Below 각각의 그룹을 SortKey 기준으로 정렬
2. 연속 동일 BatchKey 런마다 batchIndex 부여
3. 그룹 단위로 offset을 더해 전역 유니크 ID 화 (Above/Below는 char로 분리되어 있어 절대 merge되지 않음)
4. Hierarchy 순서로 prefix distinct-count 누적
5. 프리팹마다 1로 리셋

11~12 컬럼(전체 배치 수)은 프리팹 총합 그대로.

---

## 9. 도메인 규칙 (참조)

### BatchKey
`[sortingLayerID, sharedMaterial, renderMode, mesh]`
이 키들이 모두 같으면 한 배치로 묶임.

### SortKey
`[sortingLayerID, orderInLayer, sortingFudge, hierarchyOrder]`
실제 그리는 순서 결정.

### HierarchyOrder
같은 GameObject에 Particle + Trail이 공존하면 `Trail = Particle + 0.5` (Trail이 시각적으로 앞).

### 보장되는 불변식 (런 기반 할당으로 항상)
1. **OrderInLayer 단조성**: Hierarchy에서 인접한 두 오브젝트 A(위), B(아래)에 대해
   `B.OrderInLayer >= A.OrderInLayer`. 같은 런이면 등호, 런 경계면 엄격히 증가.
2. **Fudge 단조성**: 같은 런 내 두 오브젝트 A(위), B(아래)에 대해
   `B.sortingFudge < A.sortingFudge`. 런 최하단은 항상 0.

### Material 런 정의
Hierarchy 순서(위→아래)로 정렬한 뒤 **연속된 동일 Material 구간**을 하나의 런으로 봄. 같은 Material이라도 중간에 다른 Material이 끼면 별도 런 (= 인터리브 경고 트리거).

---

## 10. 트러블슈팅 / FAQ

### Q. 메뉴가 안 보여요
- Package Manager에서 `Particle Auto Sorting 1.0.0+` 가 설치되었는지 확인
- Unity 6000.0 미만이면 미지원 (`package.json` 의 `unity` 필드)
- Console에 컴파일 에러가 있으면 메뉴가 안 뜸 → 에러 먼저 해결

### Q. 적용을 눌렀는데 변경이 디스크에 안 남아요
- 오버플로(`HasOverflow`)가 있는 프리팹이 하나라도 있으면 전체 적용이 차단됨
- StatusBar / 콘솔 메시지를 확인

### Q. Ctrl+Z 가 한 번에 다 되돌리지 않아요
- 적용은 단일 Undo 그룹으로 묶이므로 Ctrl+Z 한 번이 정상
- 만약 분할되어 있다면 적용 도중 다른 Undo 발생 가능성 있으니 적용 직후 다른 작업 전에 Undo

### Q. 같은 Material인데 인터리브 경고가 떠요
- Hierarchy 상에서 다른 Material이 사이에 끼어 있다는 뜻
- 수정 모드 ON → Sibling 드래그로 같은 Material끼리 인접시킨 뒤 재계산

### Q. CSV 한글이 깨져요
- 도구는 UTF-8 BOM으로 출력. Excel/Sheets 모두 정상
- 만약 깨지면 텍스트 에디터로 인코딩이 UTF-8 BOM인지 확인

### Q. Above/Below가 없는 프리팹은?
- 분석 결과가 비어 있거나 경고만 표시됨
- 도구 사용 전제 조건 (프리팹 구조)을 다시 확인

### Q. UPM 패키지를 업데이트하려면?
- Package Manager에서 패키지 선택 → Update 버튼
- 또는 `manifest.json` 의 `#태그` 부분을 변경하고 Unity 재로드

### Q. 다시 첫 코드로 돌리고 싶어요
- `manifest.json` 에서 `#v1.0.0` 같은 특정 버전을 명시
- 또는 도구가 적용한 변경은 Ctrl+Z로 롤백 (단, 에디터 종료 전까지)

---

## 추가 자료

- [README](../README.md) — 설치 / 빠른 시작
- [CHANGELOG](../CHANGELOG.md) — 버전별 변경
- 이슈 / 기여: https://github.com/gonggong77/particleAutoSorting
