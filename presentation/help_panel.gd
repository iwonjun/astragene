class_name HelpPanel
extends RefCounted
## "게임 방법" text shared by the menu and the in-game F1 overlay.

const TEXT := """[b]목표[/b]  육지의 80%를 차지하거나(동맹 합산 가능), 제한 시간이 끝날 때 가장 넓은 영토를 가진 쪽이 이깁니다.

[b]시작[/b]  처음 10초 동안 지도를 클릭해 시작 위치를 고릅니다. 고르지 않으면 무작위로 배치됩니다.

[b]인구 = 병력[/b]  인구는 영토 크기만큼 늘어나고, 상한의 절반쯤일 때 가장 빨리 늘어납니다. 너무 많이 쓰면 성장도 느려집니다.

[b]공격[/b]  아래 [color=#55d9c7]병력 비율 슬라이더[/color](Q/E)로 보낼 비율을 정하고 인접한 빈 땅이나 다른 나라 땅을 [b]좌클릭[/b]합니다. 병력이 국경을 따라 번지며 땅을 차지합니다. 산·언덕·숲은 비싸고, 병력이 밀집한 나라는 뚫기 어렵습니다.

[b]건물[/b]  (B) 도시=인구 상한, 요새=주변 방어 ×1.5, 시장=금 수입, 연구소=시대 게이지, 항구=상륙선(중세~), 사일로=미사일·핵(현대~). 내 땅을 클릭해 짓습니다. 같은 건물은 지을수록 비싸집니다.

[b]시대[/b]  구석기 → 신석기 → 고대 → 중세 → 근세 → 산업 → 현대 → 미래. 게이지가 차면 다음 시대로 넘어가며 특성 3개 중 하나를 고릅니다. 시대가 1 앞설 때마다 전투력 +25%(최대 +60%). 뒤처진 나라는 게이지를 더 빨리 채웁니다.

[b]외교[/b]  다른 나라를 [b]우클릭[/b]하면 조약을 제안할 수 있습니다: 불가침·동맹·휴전·무역·조공·기술 공유. 채팅 창에서 전체/동맹/1:1 대화를 하고, [b]Alt+클릭[/b]으로 지도 위치를 찍어 보낼 수 있습니다.

[b]배신[/b]  조약 중인 상대를 공격하면 조약이 모두 깨지고 60초 동안 [color=#ff6b61]배신자[/color]가 됩니다(방어 -20%, AI가 믿지 않음). 불가침은 '파기 예고' 후 30초가 지나면 배신 없이 공격할 수 있습니다.

[b]조작[/b]  WASD/방향키·가장자리·가운데 드래그 = 지도 이동, 휠 = 확대, Space = 수도로, Enter = 채팅, Esc = 취소, F1 = 도움말."""


static func make() -> PanelContainer:
	var text := RichTextLabel.new()
	text.bbcode_enabled = true
	text.fit_content = true
	text.custom_minimum_size = Vector2(640, 0)
	text.text = TEXT
	text.add_theme_font_size_override("normal_font_size", 15)
	text.add_theme_font_size_override("bold_font_size", 15)
	return UI.panel(text)
