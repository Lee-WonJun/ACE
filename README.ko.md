# ACE

**ACE (Algebraic Call Effects)** 는 **모든 함수 호출을 “대수적 효과(effect)”로 해석하는 실험적 언어**입니다.

> 함수 호출 = 값 계산
> ACE에서는
> **함수 호출 = 효과 수행(effect perform)**

🌐 **[온라인에서 사용해보기](https://lee-wonjun.github.io/ACE/)** 
---

## 한 줄 요약

> **ACE는 “함수 호출”을 중심으로
> AOP, DI, 미들웨어, 모킹을 하나의 핸들러 모델로 통합하려는 아이디어 실험입니다.**

---

## 핵심 아이디어

### `defn`은 함수 정의가 아니다

ACE에서 `defn`은 **함수를 정의하지 않습니다.**
대신 다음 두 가지를 **컴파일 타임(또는 전처리 단계)** 에 수행합니다.

```ace
defn double(n) : Number { n * 2 }
```

위 한 줄은:

1. `double`이라는 **대수적 효과(effect)** 를 선언하고
2. `{ n * 2 }`를 **루트 핸들러(root handler)** 로 등록합니다

즉,

* `double(10)`은 “함수 호출”이 아니라
* **`double` 효과를 수행(perform)** 하는 표현입니다

런타임에서는 **핸들러 스택을 위에서 아래로 탐색**하여
누가 이 효과를 처리할지 결정합니다.

---

## ACE의 세계관

| 전통적인 언어    | ACE      |
| ---------- | -------- |
| 함수 호출      | 효과 수행    |
| 함수 구현      | 루트 핸들러   |
| 오버라이딩 / DI | 핸들러 가로채기 |
| 미들웨어       | 중첩된 핸들러  |
| 모킹         | 테스트용 핸들러 |

---

## 문법

### 1. 효과 정의 (`defn`)

```ace
// 명시적 효과 (인터페이스만 선언)
defn `Print` (msg) : Unit

// 암시적 효과 (효과 + 기본 핸들러)
defn add(x, y) : Number { x + y }
```

* **백틱 이름 (`Print`)**

  * 순수 효과 선언
  * 기본 구현(루트 핸들러) 없음
* **일반 이름 (`add`)**

  * 효과 + 루트 핸들러가 함께 정의됨

---

### 2. 효과 수행 (모든 호출은 효과)

ACE에는 `perform` 키워드가 없습니다.
**모든 호출이 곧 효과 트리거입니다.**

```ace
`Print`("hello")   // 명시적 효과 수행
add(1, 2)          // 이것도 효과 수행 (루트 핸들러가 처리)
```

---

### 3. 효과 핸들링 (`handle / with`)

```ace
handle {
    let x = getValue()
    x + 1
} with (getValue) {
    continue k (42)
}
```

* `handle { ... }` : 효과를 발생시키는 코드
* `with (effect)` : 특정 효과를 가로채는 핸들러
* `continue k` : 중단된 계산을 재개

---

### 4. `v` 변수 — 업스트림 위임

핸들러 내부에서 `v`는 다음을 의미합니다:

> **“내가 이 효과를 가로채지 않았다면,
> 위쪽 핸들러(또는 루트 핸들러)는 무엇을 반환했을까?”**

```ace
defn getValue() : Number { 100 }

handle {
    getValue()
} with (getValue) {
    continue k (v + 1)
}
```

* `v = 100` (루트 핸들러의 결과)
* 최종 결과 = `101`

---

### 5. 제어 흐름 요약

| 표현식                 | 업스트림 실행 | 의미                  |
| ------------------- | ------- | ------------------- |
| `continue k`        | ❌       | `Unit`으로 재개         |
| `continue k (expr)` | ❌       | `expr`로 재개          |
| `continue k v`      | ✅       | 업스트림에 위임 후 그 결과로 재개 |

---

## 설계 목표 (POC 수준)

> ⚠️ **ACE는 연구용 / 실험용 POC입니다.**
> 실제 언어로 구현할 계획은 없으며,
> *“이런 모델이 가능할까?”* 를 검증하기 위한 아이디어 실험입니다.

또한 ACE는:

* Eff / Koka / Unison 같은
  **정교한 대수적 이펙트 시스템을 목표로 하지 않습니다**
* 부수효과 모델링보다는
  **“함수 호출에 대한 범용 추상화”** 로서 핸들러를 실험합니다
* 수학적으로 엄밀하게 설계되지 않았으며,
  많은 문제점이 있음을 인지하고 있습니다

---

## 주요 아이디어

### 1. 범용 추상화로서의 효과

모든 호출 지점이 효과 지점이기 때문에,
다음 패턴들이 **하나의 개념으로 수렴**합니다:

* AOP (로깅, 트레이싱, 메트릭)
* 의존성 주입 (DI)
* 테스트 모킹
* 미들웨어 / 인터셉터


---

### 2. `defn` = 컴파일 타임 효과 등록

```text
defn name(args) { body }
≡
register_effect("name", args)
register_root_handler("name", body)
```

런타임 동작:

1. 핸들러 스택을 위 → 아래로 탐색
2. 핸들러가 있으면 처리
3. 없으면 루트 핸들러로 폴스루
4. 루트 핸들러도 없으면 런타임 에러

---

### 3. 정적 효과 추론 (아이디어)

```ace
defn log(msg) : Unit with `Print` {
    `Print`(msg)
}
```

* `with` 절은 **이 함수가 트리거할 수 있는 효과**를 선언
* 이 정보를 이용해:

  * `main` 종료 시 모든 효과가 처리되었는지 검증 가능

---

### 4. `sealed` 함수

```ace
sealed defn add(x, y) { x + y }
```

* `sealed` 함수는 **가로챌 수 없음**
* 성능 / 보안이 중요한 코드에 사용

---

### 5. 백틱 전용 가로채기 (선택적 설계)

```ace
add(1, 2)     // 항상 루트 핸들러 호출
`add`(1, 2)   // 핸들러로 가로챌 수 있음
```

* 일반 호출 = 순수 함수
* 백틱 호출 = 효과

---

### 6. 핸들러 재진입 방지

```ace
handle {
    foo()
} with (foo) {
    foo()   // ❌ 에러: 무한 재귀 감지
}
```

런타임이 **자기 자신을 다시 가로채는 호출을 감지**합니다.

---

### 7. 렉시컬 스코프 핸들러

```ace
handle { ... } with (effect) { ... }
```

* 핸들러는 **구문 블록**
* 일급 값이 아님
* 정적 분석 가능:

  * 어떤 효과가 어디서 처리되는지 추적 가능

---

## 현재 구현

* **F#**
* **Bolero (WebAssembly)**
* **XParsec**

```bash
dotnet build AceLang.sln
dotnet run --project src/AceLang.Client/AceLang.Client.fsproj
dotnet test tests/AceLang.Tests
```

```text
AceLang/
├── src/AceLang.Client/
│   ├── AST.fs           # AST 정의
│   ├── Parser.fs        # XParsec 문법
│   ├── Interpreter.fs  # Request / Done / Error 평가
│   └── Main.fs         # Bolero Web UI
└── tests/AceLang.Tests/
```

---

## 영감

* [Eff](https://www.eff-lang.org/)
* [Koka](https://koka-lang.github.io/)
* [Unison](https://www.unison-lang.org/)
