# 날라액션 v1.0.0 정식 릴리즈 체크리스트

정식 릴리즈는 다음 조건을 모두 충족해야 한다.

- Windows x64 Release 빌드 성공
- self-contained single-file EXE 생성
- 제품명 `날라액션`
- 파일/제품 버전 `1.0.0`
- 프로그램/창 아이콘 적용
- 인트로 5초 페이드 인/아웃 후 중앙 메인창 표시
- 마우스 이동/버튼/휠 녹화 및 재생
- 키보드 일반키/조합키 녹화 및 재생
- 텍스트 입력 단계
- 단계 추가/편집/삭제/위아래 이동/비활성화
- 저장 없이 즉시 실행
- `.nlaction` 저장/불러오기
- 앱 자체 UI 입력 녹화 제외
- 전역 긴급중지 Ctrl+Shift+F12
- 비정상 액션 파일 fail-closed 검증
- CI 빌드 및 배포 패키지 검증
- ZIP 및 SHA-256 생성
- `Eoingtilab/nalapps-releases` GitHub Release 등록
- EDD 등록 버전과 Git tag 일치

정식판 태그: `v1.0.0`
