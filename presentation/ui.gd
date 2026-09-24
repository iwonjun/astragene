class_name UI
extends RefCounted
## Theme and small widget helpers. All UI is built in code (no hand-edited scenes to keep in sync).

const ACCENT := Color(0.33, 0.85, 0.78)
const GOLD := Color(0.95, 0.8, 0.35)
const DANGER := Color(1.0, 0.42, 0.38)
const MUTED := Color(0.62, 0.7, 0.78)
const PANEL := Color(0.055, 0.08, 0.12, 0.9)

static var _theme: Theme


static func theme() -> Theme:
	if _theme != null:
		return _theme
	var t := Theme.new()
	t.default_font = load("res://assets/fonts/Pretendard-Regular.otf")
	t.default_font_size = 16
	var panel := _box(PANEL, Color(0.2, 0.45, 0.5, 0.8), 1, 8)
	t.set_stylebox("panel", "PanelContainer", panel)
	t.set_stylebox("panel", "Panel", panel)
	t.set_stylebox("normal", "Button", _box(Color(0.1, 0.16, 0.22, 0.95), Color(0.25, 0.5, 0.55, 0.9), 1, 6))
	t.set_stylebox("hover", "Button", _box(Color(0.14, 0.27, 0.32, 1), ACCENT, 1, 6))
	t.set_stylebox("pressed", "Button", _box(Color(0.2, 0.42, 0.45, 1), ACCENT, 2, 6))
	t.set_stylebox("disabled", "Button", _box(Color(0.08, 0.1, 0.13, 0.9), Color(0.2, 0.25, 0.3), 1, 6))
	t.set_stylebox("focus", "Button", _box(Color(0, 0, 0, 0), ACCENT, 1, 6))
	t.set_color("font_disabled_color", "Button", Color(0.45, 0.5, 0.55))
	t.set_stylebox("normal", "LineEdit", _box(Color(0.03, 0.05, 0.08, 0.95), Color(0.25, 0.4, 0.45), 1, 5))
	t.set_stylebox("focus", "LineEdit", _box(Color(0, 0, 0, 0), ACCENT, 1, 5))
	t.set_stylebox("normal", "OptionButton", _box(Color(0.1, 0.16, 0.22, 0.95), Color(0.25, 0.5, 0.55, 0.9), 1, 6))
	t.set_stylebox("hover", "OptionButton", _box(Color(0.14, 0.27, 0.32, 1), ACCENT, 1, 6))
	t.set_stylebox("background", "ProgressBar", _box(Color(0.03, 0.05, 0.08), Color(0.2, 0.3, 0.35), 1, 4))
	t.set_stylebox("fill", "ProgressBar", _box(ACCENT, ACCENT, 0, 4))
	t.set_stylebox("panel", "TooltipPanel", _box(Color(0.02, 0.04, 0.06, 0.97), ACCENT, 1, 4))
	t.set_color("font_color", "Label", Color(0.9, 0.94, 0.97))
	_theme = t
	return t


static func _box(bg: Color, border: Color, width: int, radius: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(width)
	s.set_corner_radius_all(radius)
	s.content_margin_left = 10
	s.content_margin_right = 10
	s.content_margin_top = 6
	s.content_margin_bottom = 6
	return s


static func label(text: String, size: int = 16, color: Color = Color(0.9, 0.94, 0.97)) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	return l


static func button(text: String, callback: Callable, tooltip: String = "") -> Button:
	var b := Button.new()
	b.text = text
	b.tooltip_text = tooltip
	b.focus_mode = Control.FOCUS_NONE
	b.pressed.connect(callback)
	return b


static func panel(content: Control) -> PanelContainer:
	var p := PanelContainer.new()
	p.add_child(content)
	return p


static func vbox(sep: int = 8) -> VBoxContainer:
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", sep)
	return v


static func hbox(sep: int = 8) -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", sep)
	return h


static func option(items: Array, selected: int = 0) -> OptionButton:
	var o := OptionButton.new()
	o.focus_mode = Control.FOCUS_NONE
	for it in items:
		o.add_item(str(it))
	o.select(selected)
	return o


static func spin(min_v: int, max_v: int, value: int, step: int = 1) -> SpinBox:
	var s := SpinBox.new()
	s.min_value = min_v
	s.max_value = max_v
	s.step = step
	s.value = value
	return s


## Pins c to an anchor point (0..1 of the parent) at an offset; the control then grows from that
## point by its minimum size in the given directions (-1 begin, 0 both, 1 end).
static func place(c: Control, ax: float, ay: float, x: float, y: float, grow_x: int = 1, grow_y: int = 1) -> void:
	c.anchor_left = ax
	c.anchor_right = ax
	c.anchor_top = ay
	c.anchor_bottom = ay
	c.offset_left = x
	c.offset_right = x
	c.offset_top = y
	c.offset_bottom = y
	c.grow_horizontal = [Control.GROW_DIRECTION_BEGIN, Control.GROW_DIRECTION_BOTH, Control.GROW_DIRECTION_END][grow_x + 1]
	c.grow_vertical = [Control.GROW_DIRECTION_BEGIN, Control.GROW_DIRECTION_BOTH, Control.GROW_DIRECTION_END][grow_y + 1]


static func is_web() -> bool:
	return OS.has_feature("web")
