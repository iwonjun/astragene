class_name ChatFilter
extends RefCounted
## Minimal profanity masking for public rooms (off by default in private friend rooms, design.md 8.5).
## Jamo-split variants are matched after removing spaces and punctuation between letters.

const WORDS := ["씨발", "시발", "ㅅㅂ", "병신", "ㅂㅅ", "개새끼", "좆", "fuck", "shit", "bitch"]
var enabled := false


func clean(text: String) -> String:
	if not enabled:
		return text
	var squeezed := ""
	var map := PackedInt32Array()
	for i in text.length():
		var c := text[i]
		if c == " " or c == "." or c == "*" or c == "_" or c == "-":
			continue
		squeezed += c.to_lower()
		map.append(i)
	var chars := text.split("")
	for w in WORDS:
		var from := 0
		while true:
			var at := squeezed.find(w, from)
			if at < 0:
				break
			for k in w.length():
				chars[map[at + k]] = "*"
			from = at + w.length()
	return "".join(chars)
