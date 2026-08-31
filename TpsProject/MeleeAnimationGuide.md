# 근접 공격 애니메이션

## 게임에서 확인

MainScene을 실행하고 숫자 1을 누른 다음 마우스 왼쪽 버튼을 클릭합니다.
오른손의 무기를 들어 올렸다가 앞쪽으로 내려치고 준비 자세로 돌아옵니다.
숫자 2를 누르면 총으로 돌아갑니다. 컴포넌트를 직접 추가할 필요는 없습니다.

## 동작 원리

- `ThirdPersonShooter`가 `PlayerLoadout`과 `PlayerMelee`를 자동으로 준비합니다.
- `PlayerMeleeAnimation`은 실제 Animator에 붙으며, Humanoid 근육 곡선으로 팔과 상체를 움직입니다.
- 기존 `SciFiWarrior.controller`에 `PlayerMelee` 레이어를 추가했습니다. 상체 마스크를 사용하므로 하체의 걷기/달리기는 기존 레이어가 담당합니다.
- 레이어의 기본 가중치는 0입니다. 플레이어가 근접 무기를 선택할 때만 활성화하므로 같은 Animator Controller를 쓰는 적에게는 적용되지 않습니다.
- 전체 동작은 0.65초이며 공격 클립의 0.28초 `OnMeleeImpact` 이벤트에서 범위/방향/벽을 검사하고 한 번만 피해를 줍니다.
- 무기 전환이나 사망은 예약된 피해를 취소합니다. 일시정지는 애니메이션과 공격 시간을 함께 멈춥니다.
- 무기는 손에 고정되며, 손 뼈대의 0.01 스케일을 보정해 작아지지 않도록 했습니다.

## 자세 수정

1. Play Mode를 종료합니다.
2. Project 창에서 `Assets/Resources/PlayerMeleeAttack.anim`을 찾습니다.
3. MainScene의 플레이어 중 Animator가 있는 오브젝트를 선택하고 `Window > Animation > Animation`을 엽니다.
4. 클립 목록에서 `PlayerMeleeAttack`을 선택하고 Preview로 확인합니다. 필요하면 Curves에서 팔/상체 근육 곡선 값을 조절합니다.
5. 주요 자세는 0초 준비, 0.18초 들어 올리기, 0.28초 타격, 0.40초 후속 동작, 0.65초 복귀입니다.
6. 대기 자세는 `PlayerMeleeReady.anim`에서 수정합니다. 공격 클립의 시작/끝과 같은 자세로 맞추면 전환이 자연스럽습니다.

타격 이벤트를 삭제하지 마세요. 이벤트 시간을 옮기면 실제 피해 시점도 바뀝니다.
전체 길이를 바꾸려면 `PlayerMeleeAnimation.Duration`과 공격 간격도 함께 조정해야 합니다.

`Tools > Animation > Rebuild Player Melee Clips`는 코드에 정의한 초기 자세로 클립을 다시 만듭니다.
직접 편집한 클립도 덮어쓰므로 수동 편집을 유지하려면 실행하지 마세요.

## 확인한 항목

분리된 Unity 테스트 프로젝트에서 실제 HPCharacter의 네 자세를 렌더링했습니다.
상체 마스크, 손 부착 크기, 기존 레이어 보존, 중복 생성 방지, 타격 이벤트, 중복 피해 방지,
전환 취소, 일시정지/재개, 사망 취소 및 기존 90발/재장전/스테이지 초기화 테스트를 통과했습니다.
