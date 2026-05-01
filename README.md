# Particle Auto Sorting

Unity Editor 확장 도구. 파티클 시스템의 **Order in Layer**와 **Sorting Fudge**를 자동으로 세팅하여 두 가지를 동시에 달성합니다.

1. **배치 수(Draw Call) 최소화** — 동일 Material 런(run) 단위로 묶어 GPU Instancing 친화적으로 정렬
2. **Hierarchy 하단 우선 렌더링** — 같은 부모의 자식 중 아래에 있을수록 시각적으로 앞에 그려지도록 보장

외부 패키지 의존이 전혀 없는 순수 Editor 도구입니다.

---

## 요구사항

- Unity **6000.0** 이상 (개발/검증 환경: 6000.0.62f1)

---

## 설치 방법

### 방법 1: Unity Package Manager UI (권장)

1. Unity 메뉴: **Window > Package Manager**
2. 좌측 상단 **`+`** 버튼 → **"Install package from git URL..."**
3. 다음 URL 입력 후 **Add**

```
https://github.com/gonggong77/particleAutoSorting.git
```

### 방법 2: `manifest.json` 직접 편집

프로젝트의 `Packages/manifest.json` 의 `dependencies` 블록에 한 줄 추가:

```json
{
  "dependencies": {
    "com.yurieiyi.particle-auto-sorting": "https://github.com/gonggong77/particleAutoSorting.git"
  }
}
```

### 특정 버전 고정

태그(`v1.0.0` 등)로 버전을 고정하려면 git URL 끝에 `#태그`를 붙입니다.

```json
"com.yurieiyi.particle-auto-sorting": "https://github.com/gonggong77/particleAutoSorting.git#v1.0.0"
```

---

## 사용법

설치 후 Unity 상단 메뉴에서 열기:

```
Tools > particle auto sorting
```

### 기본 흐름

1. 창 좌측 **DropZone**에 분석할 프리팹들을 Hierarchy 또는 Project 창에서 드래그앤드롭
2. 프리팹마다 자동으로 분석되어 `Above` / `Below` 자식의 ParticleSystemRenderer + TrailRenderer가 표로 노출
3. 상단 **Char Sorting Order** 와 (수정 모드 ON 시) **Fudge 간격** 조정
4. **[재계산]** (또는 수정 모드 시 **[미리보기]**) 으로 메모리상 OiL/Fudge 미리보기
5. **[적용]** 으로 모든 프리팹에 일괄 저장 (Undo 그룹으로 묶여 Ctrl+Z 한 번에 롤백 가능)
6. 필요 시 하단 CSV 패널로 분석 리포트 내보내기

### 핵심 규칙

- **프리팹 구조 가정**: 루트 아래 `Above` / `Below` 자식이 있고, 각각의 자식들이 char(캐릭터) 위/아래 영역
- **OiL 할당**: Above = `charSortingOrder + 1, +2, …, +N` / Below = `charSortingOrder - N, …, -1`
- **Fudge 할당**: 동일 Material 런 내에서 `(size - 1 - rank) * step`, default step=30
- **인터리브 경고**: 동일 Material이 다른 Material로 끊겨 여러 런으로 쪼개진 경우 pill로 알림 (배치 수 최적이 아님)
- **null Material / 비활성 오브젝트**: silent skip 또는 경고 표시
- **오버플로**: short 범위(-32768~32767)를 넘으면 적용 차단 (clamp 안 함)

### 수정 모드

상단 토글로 ON 시:
- Hierarchy Sibling 드래그앤드롭(같은 부모 내) 가능
- **[재계산]** 버튼이 **[미리보기]** 로 토글, 메모리만 갱신 (디스크 무변경)
- 재정렬 후 미리보기 미클릭 행에 "↻ 미리보기 필요" 뱃지

---

## 메뉴 위치

```
Tools > particle auto sorting
```

도구 진입점 클래스: `ParticleAutoSorting.Editor.ParticleAutoSortingWindow`

---

## 폴더 구조

```
Editor/
├── ParticleAutoSorting.Editor.asmdef
├── ParticleAutoSortingWindow.cs            # 메인 진입점, 전역 상태
├── ParticleAutoSortingWindow.List.cs       # 프리팹 리스트, 경고 pill
├── ParticleAutoSortingWindow.Table.cs      # Above/Below Renderer 테이블
├── ParticleAutoSortingWindow.Bottom.cs     # 하단 버튼 + CSV 패널
├── Analysis/
│   ├── PrefabAnalyzer.cs                   # 프리팹 스캔 (PSR + Trail)
│   └── BatchCounter.cs                     # 배치 수 계산
├── Optimization/
│   └── SortingOptimizer.cs                 # OiL/Fudge 할당 + 인터리브 감지
├── Apply/
│   ├── PrefabApplier.cs                    # Undo + SavePrefabAsset
│   └── HierarchyReorderer.cs               # Sibling 순서만 재정렬
├── Report/
│   └── CsvReportExporter.cs                # UTF-8 BOM CSV
└── Data/
    ├── RendererInfo.cs
    ├── PrefabData.cs
    └── MaterialGroupInfo.cs
```

---

## 라이선스

[MIT](LICENSE)

---

## 변경 이력

[CHANGELOG.md](CHANGELOG.md)

---

## 기여

이슈와 PR 환영합니다: https://github.com/gonggong77/particleAutoSorting
