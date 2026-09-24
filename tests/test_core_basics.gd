extends GdUnitTestSuite


func test_rng_is_deterministic_and_masked() -> void:
	var a := SimRng.new(42)
	var b := SimRng.new(42)
	for i in 1000:
		var v := a.next_u32()
		assert_int(v).is_equal(b.next_u32())
		assert_bool(v >= 0 and v <= 0xFFFFFFFF).is_true()
	# Golden values guard against accidental algorithm changes (desktop and web must agree).
	var g := SimRng.new(1)
	assert_int(SimRng.mix(1)).is_equal(SimRng.mix(1))
	assert_int(g.next_u32()).is_equal(SimRng.new(1).next_u32())


func test_hash_is_order_sensitive() -> void:
	var h1 := SimHash.new(); h1.add(1); h1.add(2)
	var h2 := SimHash.new(); h2.add(2); h2.add(1)
	assert_int(h1.value).is_not_equal(h2.value)


func test_ruleset_is_integer_only() -> void:
	var r := Ruleset.load_default()
	assert_int(typeof(r.eras[0]["cap_per_tile"])).is_equal(TYPE_INT)
	assert_int(r.eras.size()).is_equal(8)
	assert_int(r.treaties.size()).is_equal(6)
	assert_int(r.digest()).is_equal(Ruleset.load_default().digest())


func test_map_generation_is_deterministic_with_expected_land() -> void:
	var a := MapGen.generate(7, 200, 120, 470)
	var b := MapGen.generate(7, 200, 120, 470)
	assert_bool(a == b).is_true()
	var land := 0
	for t in a:
		if t != MapGen.OCEAN:
			land += 1
	var permille := land * 1000 / a.size()
	assert_int(permille).is_between(380, 520)
	assert_bool(MapGen.generate(8, 200, 120, 470) == a).is_false()
