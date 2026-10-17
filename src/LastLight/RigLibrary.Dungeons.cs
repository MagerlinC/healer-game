using Godot;
using static healerfantasy.LastLight.RigDefinition;

namespace healerfantasy.LastLight;

/// <summary>
/// Darkness rigs for every dungeon after the Ancient Keep. Each dungeon has its
/// own creature language and one signature glow inside the black:
///   • Forsaken Citadel — rusted war-machines and carrion fliers; ember and rust.
///   • Castle of Blood  — gaunt aristocrats; blood-red eyes and veins.
///   • Sanctum of Stars — the void wearing star-shaped holes; cold starlight.
///   • Guttering Spire  — melted, dripping shapes; warm tallow and candle flames.
///   • The Abyss        — darkness itself, too vast to see whole; two pale eyes.
/// Same authoring rules as <see cref="RigLibrary"/>: feet at (0,0), facing right.
/// </summary>
public static partial class RigLibrary
{
	// ── Forsaken Citadel ─────────────────────────────────────────────────────
	static readonly Color Ember = new(1f, 0.52f, 0.16f);
	static readonly Color Rust = new(0.3f, 0.13f, 0.06f);
	static readonly Color Smoke = new(0.2f, 0.19f, 0.18f);

	// ── Castle of Blood ──────────────────────────────────────────────────────
	static readonly Color BloodEye = new(1f, 0.16f, 0.1f);
	static readonly Color BloodVein = new(0.55f, 0.04f, 0.05f);
	static readonly Color BloodPool = new(0.07f, 0f, 0.005f, 0.94f);

	// ── Sanctum of Stars ─────────────────────────────────────────────────────
	static readonly Color Star = new(0.86f, 0.9f, 1f);
	static readonly Color StarDim = new(0.5f, 0.55f, 0.8f);

	// ── Guttering Spire ──────────────────────────────────────────────────────
	static readonly Color Tallow = new(1f, 0.86f, 0.6f);
	static readonly Color TallowDim = new(0.42f, 0.31f, 0.18f);
	static readonly Color TallowFlame = new(1f, 0.66f, 0.24f);
	static readonly Color TallowPool = new(0.05f, 0.03f, 0.01f, 0.92f);

	// ═════════════════════════════════════════════════════════════════════════
	// THE FORSAKEN CITADEL
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>Flying Demon → a carrion-winged demon with rust-ribbed wings and ember eyes.</summary>
	public static RigDefinition FlyingDemon() => new RigDefinition
		{
			Id = "flying-demon", Mode = RigMode.Darkness, Height = 170f, Width = 120f, Scale = 0.58f,
			SwingAngle = 1.3f, Lunge = 14f, Breath = 0.8f, Pool = new Vector2(80, 12),
			Hover = true, HoverHeight = 45f
		}
		.Joint("body", "", 0, -60)
		.Joint("head", "body", 22, -108)
		.Joint("arm", "body", 18, -88)
		.Joint("off", "body", -10, -80)
		.Joint("wingBack", "body", -4, -96)
		.Joint("wingFront", "body", 6, -96)
		// wings: ragged membranes on rust ribs
		.Part("wingBack", Dark2, "-4,-96 -62,-152 -112,-146 -100,-124 -124,-104 -96,-98 -108,-70 -76,-82 -72,-60 -46,-78 -10,-82", -3)
		.Part("wingBack", Rust, Thick(2f, "-4,-96 -62,-152"), -3)
		.Part("wingBack", Rust, Thick(1.5f, "-30,-118 -110,-104"), -3)
		.Part("wingFront", Dark, "6,-96 -30,-168 -82,-174 -66,-152 -88,-130 -62,-126 -64,-100 -40,-110 -26,-86 2,-84", -1)
		.Part("wingFront", Rust, Thick(2f, "6,-96 -30,-168"), -1)
		.Part("wingFront", Rust, Thick(1.5f, "-12,-128 -80,-140"), -1)
		// dangling legs, talons
		.Part("", Dark, "-12,-32 -8,-62 -1,-62 -4,-30")
		.Part("", Dark, "6,-28 4,-60 12,-60 16,-26")
		.Part("", Dark, "-12,-32 -18,-22 -10,-26 -6,-18 -4,-30")
		.Part("", Dark, "6,-28 2,-18 10,-22 14,-14 16,-26")
		// hunched torso
		.Part("body", Dark, "-18,-55 -22,-90 -8,-114 12,-118 28,-102 24,-72 10,-52")
		.Part("off", Dark, "-6,-90 -26,-66 -20,-60 -2,-80", -1)
		// horned head, ember eyes
		.Part("head", Dark, "12,-106 14,-126 28,-134 42,-126 46,-112 32,-102")
		.Part("head", Dark, "16,-126 4,-150 22,-130")
		.Part("head", Dark, "30,-130 40,-154 37,-128")
		.Part("head", Void, "32,-122 46,-118 44,-110 34,-112")
		.Part("head", Ember, Circle(37f, -117f, 2.4f, 8))
		.Part("head", Ember, Circle(42f, -116f, 1.8f, 8))
		// claw arm
		.Part("arm", Dark, "16,-94 50,-82 62,-66 52,-62 44,-74 14,-82", 1)
		.Part("arm", Dark, "58,-68 72,-56 60,-62", 1)
		.Part("arm", Dark, "52,-64 62,-50 54,-58", 1);

	/// <summary>Mecha Golem → the Iron Penitent: a rusted war-engine with a furnace for a heart.</summary>
	public static RigDefinition MechaGolem() => new RigDefinition
		{
			Id = "mecha-golem", Mode = RigMode.Darkness, Height = 260f, Width = 160f, Scale = 0.44f,
			SwingAngle = 1.2f, Lunge = 10f, Breath = 0.4f, CastRaise = 1.4f,
			Pool = new Vector2(130, 24), PoolColor = new Color(0.05f, 0.02f, 0.01f, 0.94f)
		}
		.Joint("body", "", 0, -70)
		.Joint("head", "body", 10, -192)
		.Joint("arm", "body", 52, -172)
		.Joint("off", "body", -48, -168)
		.Joint("stack", "body", -44, -196)
		// chimney + smoke on the back
		.Part("stack", Dark2, "-56,-188 -56,-256 -34,-256 -34,-188", -2)
		.Part("stack", Rust, "-58,-258 -32,-258 -32,-250 -58,-250", -2)
		.Part("stack", Smoke, Thick(3f, "-46,-262 -40,-282 -50,-298 -42,-318"), -2)
		// stump legs
		.Part("", Dark, "-62,0 -56,-72 -16,-72 -22,0")
		.Part("", Dark, "14,0 18,-72 56,-72 64,0")
		.Part("", Dark2, "-70,0 -66,-10 -14,-10 -12,0")
		.Part("", Dark2, "10,0 12,-10 64,-10 70,0")
		// back arm
		.Part("off", Dark, "-50,-192 -86,-182 -96,-122 -72,-112 -62,-160", -1)
		.Part("off", Dark2, "-96,-122 -102,-96 -88,-102 -82,-114", -1)
		// hull, rust plates, furnace
		.Part("body", Dark, "-72,-66 -80,-152 -62,-198 62,-198 80,-152 72,-66")
		.Part("body", Rust, "-62,-152 -26,-152 -26,-112 -62,-112")
		.Part("body", Rust, "30,-180 60,-180 66,-150 30,-150")
		.Part("body", Void, "-18,-152 22,-152 22,-110 -18,-110")
		.Part("body", Ember, "-13,-148 -9,-148 -9,-114 -13,-114")
		.Part("body", Ember, "0,-148 4,-148 4,-114 0,-114")
		.Part("body", Ember, "12,-148 16,-148 16,-114 12,-114")
		// sunken helm, ember visor
		.Part("head", Dark2, "-12,-188 -14,-218 30,-218 32,-188")
		.Part("head", Ember, "-6,-206 26,-206 26,-201 -6,-201")
		// hammer arm
		.Part("arm", Dark, "44,-198 82,-188 88,-142 62,-132", 1)
		.Part("arm", Dark, "64,-138 92,-142 94,-82 72,-78", 1)
		.Part("arm", Dark2, "70,-84 112,-94 118,-52 76,-42", 1)
		.Part("arm", Rust, "72,-72 114,-80 115,-72 73,-64", 1);

	/// <summary>Flying Skull → the Pyre Skull: a huge skull trailing black smoke, embers for eyes.</summary>
	public static RigDefinition FlyingSkull() => new RigDefinition
		{
			Id = "flying-skull", Mode = RigMode.Darkness, Height = 140f, Width = 100f, Scale = 0.5f,
			SwingAngle = 0.45f, Lunge = 16f, Breath = 1.4f, CastRaise = -0.35f,
			Pool = new Vector2(55, 10), Hover = true, HoverHeight = 55f
		}
		.Joint("body", "", 0, -70)
		.Joint("arm", "body", 0, -52)
		.Joint("off", "body", -40, -80)
		// smoke trailing behind
		.Part("off", Dark2, "-36,-84 -70,-112 -96,-100 -124,-128 -102,-80 -134,-70 -96,-58 -60,-56", -2)
		.Part("off", Smoke, Thick(2f, "-60,-70 -100,-86 -128,-80"), -2)
		// cranium
		.Part("body", Dark, "-40,-58 -46,-100 -30,-128 0,-138 30,-130 46,-104 44,-70 30,-52 -30,-52")
		.Part("body", Ember, Thick(1.6f, "12,-132 6,-118 14,-108 8,-98"))
		.Part("body", Void, "-24,-102 -6,-102 -4,-84 -22,-82")
		.Part("body", Void, "6,-102 26,-104 24,-84 8,-84")
		.Part("body", Ember, Circle(-14f, -92f, 3f, 8))
		.Part("body", Ember, Circle(16f, -93f, 3f, 8))
		.Part("body", Void, "-3,-78 3,-78 0,-68")
		// jaw (opens on attack)
		.Part("arm", Dark2, "-30,-54 30,-54 26,-32 10,-24 -10,-24 -26,-32", 1)
		.Part("arm", Void, "-24,-54 -18,-46 -12,-54 -6,-46 0,-54 6,-46 12,-54 18,-46 24,-54 24,-50 -24,-50", 1);

	// ═════════════════════════════════════════════════════════════════════════
	// CASTLE OF BLOOD
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>Blood Knight → the Exsanguinated: a gaunt knight with a blood-grooved blade.</summary>
	public static RigDefinition BloodKnight() => new RigDefinition
		{
			Id = "blood-knight", Mode = RigMode.Darkness, Height = 300f, Width = 120f, Scale = 0.38f,
			SwingAngle = 1.5f, Lunge = 12f, Breath = 0.6f, Pool = new Vector2(140, 24), PoolColor = BloodPool
		}
		.Joint("body", "", 0, -90)
		.Joint("head", "body", 10, -216)
		.Joint("arm", "body", 46, -210)
		.Joint("off", "body", -36, -206)
		.Joint("cape", "body", -30, -212)
		.Part("cape", Dark2, "-30,-215 -82,-150 -102,-40 -92,6 -76,-10 -64,10 -50,-8 -34,6 -20,-60 -10,-200", -2)
		.Part("", Dark, "-40,0 -32,-96 -5,-96 -12,0")
		.Part("", Dark, "10,0 6,-96 34,-96 42,0")
		.Part("off", Dark, "-36,-210 -66,-196 -70,-150 -52,-140 -46,-186", -1)
		.Part("body", Dark, "-46,-90 -48,-170 -36,-216 30,-218 48,-170 42,-90")
		.Part("body", BloodVein, Thick(2f, "-22,-184 -10,-156 -18,-126 -8,-100"))
		.Part("body", BloodVein, Thick(1.5f, "14,-196 22,-164 12,-140"))
		.Part("head", Dark, "-6,-214 -8,-262 6,-276 26,-270 32,-256 30,-214")
		.Part("head", Dark2, "6,-274 -22,-302 -32,-290 2,-266")
		.Part("head", BloodEye, "4,-246 30,-244 30,-239 4,-241")
		.Part("arm", Dark, "28,-222 66,-212 70,-184 44,-180", 1)
		.Part("arm", Dark, "48,-186 70,-150 58,-142 40,-176", 1)
		.Part("arm", Dark2, "60,-150 68,-156 152,-38 142,-32", 1)
		.Part("arm", BloodVein, Thick(1.6f, "68,-148 142,-42"), 1)
		.Part("arm", Dark, "48,-140 80,-160 84,-152 52,-132", 1)
		.Part("arm", BloodEye, "144,-34 148,-34 147,-24 145,-24", 1);

	/// <summary>The Countess → a slender noblewoman in a bat-collared gown, holding a blood orb.</summary>
	public static RigDefinition TheCountess() => new RigDefinition
		{
			Id = "the-countess", Mode = RigMode.Darkness, Height = 290f, Width = 120f, Scale = 0.37f,
			SwingAngle = 1.0f, Lunge = 6f, Breath = 0.7f, CastRaise = 1.5f,
			Pool = new Vector2(120, 20), PoolColor = BloodPool
		}
		.Joint("body", "", 0, -80)
		.Joint("head", "body", 8, -200)
		.Joint("arm", "body", 22, -184)
		.Joint("off", "body", -14, -184)
		.Joint("train", "", -40, -40)
		.Part("train", Dark2, "-40,-60 -112,-12 -142,4 -60,6", -2)
		.Part("body", Dark,
			"-72,0 -52,-80 -26,-150 -18,-192 22,-192 30,-150 52,-80 76,0 " +
			"62,8 48,0 34,10 20,0 6,10 -8,0 -22,10 -36,0 -50,10 -62,0")
		.Part("body", new Color(0.3f, 0.02f, 0.04f), "-10,-188 12,-188 6,-120 -4,-120")
		.Part("head", Dark2, "-30,-205 -46,-246 -18,-216 0,-252 18,-216 46,-246 32,-205", -1)
		.Part("head", Dark, "-6,-200 -10,-236 0,-254 18,-252 24,-232 20,-200")
		.Part("head", Dark, "-6,-250 -2,-268 2,-253 8,-272 12,-253 18,-266 20,-250")
		.Part("head", BloodEye, Circle(14f, -232f, 1.9f, 8))
		.Part("head", BloodEye, Circle(20f, -231f, 1.6f, 8))
		.Part("off", Dark, "-12,-186 -40,-150 -36,-142 -10,-170", -1)
		.Part("arm", Dark, "18,-186 50,-150 46,-142 14,-172", 1)
		.Part("arm", BloodEye, Circle(53f, -146f, 6f, 12), 1)
		.Part("arm", new Color(1f, 0.6f, 0.5f), Circle(51f, -148f, 2f, 8), 1);

	/// <summary>The Blood Prince → a regal horror in a bat-winged cape, crowned in thorns, with a glaive.</summary>
	public static RigDefinition TheBloodPrince() => new RigDefinition
		{
			Id = "the-blood-prince", Mode = RigMode.Darkness, Height = 340f, Width = 140f, Scale = 0.38f,
			SwingAngle = 1.3f, Lunge = 14f, Breath = 0.7f, CastRaise = 1.4f,
			Pool = new Vector2(160, 26), PoolColor = BloodPool
		}
		.Joint("body", "", 0, -95)
		.Joint("head", "body", 10, -240)
		.Joint("arm", "body", 48, -226)
		.Joint("off", "body", -40, -222)
		.Joint("wingCape", "body", -10, -236)
		.Part("wingCape", Dark2,
			"-10,-236 -90,-300 -152,-282 -130,-242 -162,-202 -120,-192 -142,-122 -96,-132 -102,-40 -60,-92 -40,6 -10,-150", -2)
		.Part("wingCape", BloodVein, Thick(1.5f, "-10,-236 -120,-262 -150,-210"), -2)
		.Part("", Dark, "-40,0 -32,-100 -4,-100 -12,0")
		.Part("", Dark, "12,0 8,-100 36,-100 44,0")
		.Part("off", Dark, "-40,-236 -76,-216 -82,-160 -60,-150 -50,-200", -1)
		.Part("body", Dark, "-50,-95 -56,-186 -40,-240 36,-242 56,-186 48,-95")
		.Part("body", BloodVein, Thick(2f, "-5,-200 -24,-170 -18,-130 -30,-104"))
		.Part("body", BloodVein, Thick(2f, "-5,-200 16,-168 10,-130 22,-104"))
		.Part("body", BloodEye, Circle(-5f, -200f, 5f, 10))
		.Part("head", Dark, "-8,-238 -10,-276 6,-290 26,-284 30,-238")
		.Part("head", Dark2, "-10,-280 -16,-300 -4,-288 4,-306 10,-290 18,-308 22,-290 32,-300 28,-280")
		.Part("head", BloodEye, Circle(18f, -262f, 2.2f, 8))
		.Part("head", BloodEye, Circle(25f, -261f, 1.8f, 8))
		.Part("arm", Dark, "34,-242 72,-232 76,-202 50,-196", 1)
		.Part("arm", Dark, "54,-202 76,-162 64,-154 46,-192", 1)
		.Part("arm", Dark2, Thick(5f, "70,-60 74,-162 80,-300"), 1)
		.Part("arm", Dark2, "80,-300 68,-344 98,-312 88,-292", 1)
		.Part("arm", BloodVein, Thick(1.5f, "70,-342 96,-312"), 1);

	/// <summary>Blood Rune → a floating shard of dark stone carved with a burning sigil.</summary>
	public static RigDefinition BloodRune() => new RigDefinition
		{
			Id = "blood-rune", Mode = RigMode.Darkness, Height = 70f, Width = 44f, Scale = 0.46f,
			Breath = 1.5f
		}
		.Joint("body", "", 0, -35)
		.Part("body", Dark2, "0,-70 22,-42 16,-6 0,0 -16,-6 -22,-42")
		.Part("body", BloodEye, Thick(2.2f, "0,-58 0,-14"))
		.Part("body", BloodEye, Thick(2f, "-10,-42 10,-32"))
		.Part("body", BloodEye, Thick(2f, "-10,-26 10,-36"))
		.Part("body", BloodVein, Thick(1.2f, "-16,-48 -6,-62"))
		.Part("body", BloodVein, Thick(1.2f, "8,-8 16,-22"));

	// ═════════════════════════════════════════════════════════════════════════
	// THE SANCTUM OF STARS
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>The Nightborne → a horned knight whose cloak and armour are holes into a starfield.</summary>
	public static RigDefinition TheNightborne() => new RigDefinition
		{
			Id = "the-nightborne", Mode = RigMode.Darkness, Height = 300f, Width = 120f, Scale = 0.4f,
			SwingAngle = 1.4f, Lunge = 14f, Breath = 0.6f, Pool = new Vector2(140, 22)
		}
		.Joint("body", "", 0, -90)
		.Joint("head", "body", 14, -222)
		.Joint("arm", "body", 46, -206)
		.Joint("off", "body", -34, -202)
		.Joint("cape", "body", -26, -210)
		.Part("cape", Dark, "-26,-214 -86,-130 -102,-10 -60,6 -40,-20 -20,4 0,-150", -2)
		.Specks("cape", StarDim, -80, -150, -30, -10, 12, 7, 1.4f, -2)
		.Part("", Dark, "-38,0 -30,-96 -4,-96 -10,0")
		.Part("", Dark, "10,0 6,-96 32,-96 40,0")
		.Part("off", Dark, "-34,-206 -64,-192 -68,-148 -50,-140 -44,-182", -1)
		.Part("body", Dark, "-44,-90 -46,-170 -34,-214 30,-216 46,-170 40,-90")
		.Specks("body", Star, -36, -200, 34, -100, 14, 11, 1.3f)
		.Part("head", Dark, "-4,-212 -6,-256 8,-268 28,-262 32,-212")
		.Part("head", Dark2, "4,-262 -14,-300 -26,-306 -12,-284 0,-256")
		.Part("head", Dark2, "26,-262 44,-296 56,-300 46,-280 32,-254")
		.Part("head", Star, "8,-238 30,-236 30,-232 8,-234")
		.Part("arm", Dark, "28,-218 64,-208 68,-182 44,-176", 1)
		.Part("arm", Dark, "48,-182 68,-148 56,-140 40,-172", 1)
		.Part("arm", Dark2, "60,-148 68,-154 160,-60 150,-52", 1)
		.Part("arm", StarDim, Thick(1.2f, "66,-150 154,-58"), 1);

	/// <summary>Astral Twin → a floating haloed figure; Dawn burns pale gold, Dusk cold violet.</summary>
	public static RigDefinition AstralTwin(bool dusk)
	{
		var accent = dusk ? new Color(0.66f, 0.52f, 1f) : new Color(1f, 0.84f, 0.55f);
		var def = new RigDefinition
			{
				Id = dusk ? "astral-twin-dusk" : "astral-twin-dawn", Mode = RigMode.Darkness,
				Height = 270f, Width = 110f, Scale = 0.37f, SwingAngle = 1.0f, Lunge = 8f, CastRaise = 1.5f,
				Pool = new Vector2(80, 14), Hover = true, HoverHeight = 18f
			};
		def
			.Joint("body", "", 0, -70)
			.Joint("head", "body", 4, -196)
			.Joint("arm", "body", 22, -168)
			.Joint("off", "body", -22, -166)
			.Joint("halo", "head", 0, -228)
			.Part("body", Dark,
				"-44,0 -32,-90 -20,-176 20,-176 32,-90 48,0 36,-10 26,4 14,-8 2,6 -10,-8 -22,4 -34,-10")
			.Specks("body", new Color(accent, 0.8f), -30, -160, 30, -10, 10, dusk ? 21 : 13, 1.2f)
			.Part("head", Dark, "-18,-174 -22,-212 -6,-232 14,-232 26,-210 22,-174")
			.Part("head", Void, "-8,-206 16,-206 14,-182 -6,-182")
			.Part("head", accent, Circle(-1f, -196f, 1.8f, 8))
			.Part("head", accent, Circle(9f, -196f, 1.8f, 8))
			.Part("off", Dark, "-18,-172 -46,-130 -40,-122 -14,-156", -1)
			.Part("arm", Dark, "18,-172 50,-132 44,-124 14,-156", 1)
			.Part("arm", accent, Circle(52f, -126f, 6f, 12), 1)
			.Part("arm", new Color(1f, 1f, 1f), Circle(51f, -127f, 2f, 8), 1);
		Ring(def, "halo", accent, 0f, -228f, 46f, 2.5f, -3);
		Ring(def, "halo", new Color(accent, 0.5f), 0f, -228f, 38f, 1f, -3);
		return def;
	}

	/// <summary>
	/// That Which Swallowed the Stars → a colossal hooded void; its face is a gaping
	/// hole ringed with stolen stars, its body full of them.
	/// </summary>
	public static RigDefinition ThatWhichSwallowedTheStars() => new RigDefinition
		{
			Id = "that-which-swallowed-the-stars", Mode = RigMode.Darkness, Height = 360f, Width = 250f,
			Scale = 0.42f, SwingAngle = 1.1f, Lunge = 12f, Breath = 0.9f, CastRaise = 0.9f,
			Pool = new Vector2(200, 30),
			// the brief "cast" here is a swipe; "reveal" is just its opening pose
			AnimAliases = { ["cast"] = "attack", ["reveal"] = "idle" }
		}
		.Joint("body", "", 0, -100)
		.Joint("head", "body", 20, -260)
		.Joint("arm", "body", 80, -220)
		.Joint("off", "body", -70, -210)
		// tendril arms
		.Part("off", Dark, Thick(16f, "-70,-210 -130,-160 -150,-90 -190,-40"), -2)
		.Part("off", Dark, "-196,-46 -220,-20 -184,-34", -2)
		.Part("body", Dark,
			"-122,0 -112,-120 -84,-250 -24,-332 52,-324 102,-262 122,-140 132,0 " +
			"114,12 98,0 80,14 60,0 40,16 20,0 0,14 -20,0 -40,14 -60,0 -80,14 -100,0")
		.Specks("body", Star, -100, -300, 110, -20, 40, 5, 1.4f)
		.Specks("body", StarDim, -100, -300, 110, -20, 30, 9, 1f)
		// the face: a hole ringed with stars
		.Part("head", Void, "-12,-282 40,-292 72,-252 62,-200 20,-186 -16,-212")
		.Specks("head", Star, -4, -280, 60, -200, 8, 3, 2.4f)
		.Ring("head", Star, 26f, -240f, 52f, 1.4f)
		.Part("arm", Dark, Thick(18f, "80,-220 150,-190 186,-120 210,-60"), 1)
		.Part("arm", Dark, "206,-66 240,-40 214,-52 226,-20 202,-48", 1);

	// ═════════════════════════════════════════════════════════════════════════
	// THE GUTTERING SPIRE
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>The Tallow Queen → a tall queen in a dripping gown, crowned with lit candles, holding a candle staff.</summary>
	public static RigDefinition TheTallowQueen() => new RigDefinition
		{
			Id = "the-tallow-queen", Mode = RigMode.Darkness, Height = 300f, Width = 130f, MaxGrow = 1.05f, // the Guttering Spire camera is close; she already towers
			Scale = 0.37f, SwingAngle = 1.1f, Lunge = 8f, Breath = 0.6f, CastRaise = 1.4f,
			Pool = new Vector2(150, 24), PoolColor = TallowPool
		}
		.Joint("body", "", 0, -80)
		.Joint("head", "body", 6, -206)
		.Joint("arm", "body", 26, -186)
		.Joint("off", "body", -18, -184)
		.Part("body", Dark,
			"-80,0 -56,-80 -28,-156 -20,-196 22,-196 30,-156 56,-80 84,0 " +
			"70,6 58,0 46,8 34,0 22,8 10,0 -2,8 -14,0 -26,8 -38,0 -50,8 -64,0")
		// wax running down the gown, each run ending in a bead
		.Part("body", TallowDim, Thick(1.4f, "-30,-150 -36,-110 -42,-70"))
		.Part("body", Tallow, Circle(-42f, -66f, 2.2f, 8))
		.Part("body", TallowDim, Thick(1.4f, "0,-190 -4,-140 2,-100 -2,-40"))
		.Part("body", Tallow, Circle(-2f, -36f, 2.4f, 8))
		.Part("body", TallowDim, Thick(1.4f, "30,-150 38,-100 46,-56"))
		.Part("body", Tallow, Circle(46f, -52f, 2.2f, 8))
		.Part("head", Dark, "-8,-200 -12,-236 0,-254 18,-252 24,-232 20,-200")
		// a crown of candle stubs, burned to uneven heights
		.Part("head", Dark2, "-14,-246 -15,-280 -7,-282 -6,-252 0,-252 0,-294 8,-294 8,-252 14,-252 15,-286 23,-284 24,-246")
		.Part("head", TallowFlame, "-11,-283 -14,-291 -11,-301 -8,-291")
		.Part("head", TallowFlame, "4,-297 1,-305 4,-316 7,-305")
		.Part("head", TallowFlame, "19,-287 16,-295 19,-305 22,-295")
		.Part("head", Tallow, Circle(14f, -232f, 1.9f, 8))
		.Part("head", Tallow, Circle(20f, -231f, 1.6f, 8))
		.Part("off", Dark, "-14,-190 -44,-150 -38,-142 -10,-174", -1)
		.Part("arm", Dark, "20,-190 48,-156 42,-148 14,-176", 1)
		.Part("arm", Dark2, Thick(4f, "44,-20 46,-150 48,-250"), 1)
		// the staff is a tall candle: a drip down its side and a flame at the top
		.Part("arm", Dark2, "40,-246 40,-262 56,-262 56,-246", 1)
		.Part("arm", TallowDim, Thick(1.2f, "41,-258 39,-236 40,-222"), 1)
		.Part("arm", TallowFlame, "48,-264 39,-280 48,-306 57,-280", 1)
		.Part("arm", new Color(1f, 0.97f, 0.85f), "48,-268 44,-280 48,-294 52,-280", 1);

	/// <summary>
	/// The Tallow Hulk → a lumbering, gloomy giant of melted candles, so big that its
	/// legs vanish into the darkness below the great candle. It hunches over the
	/// arena from the left: head hung high in the corner, both hands resting on the
	/// platform's near side.
	/// Heavy brow, drooping eyes, a downturned mouth; most candles on its back are out.
	/// Authored facing right (the arena); the rig's origin is its waist, level with the platform.
	/// </summary>
	public static RigDefinition TheTallowHulk() => new RigDefinition
		{
			Id = "the-tallow-hulk", Mode = RigMode.Darkness, Height = 520f, Width = 360f, Scale = 0.5f,
			FeetOffset = 0f, BurnTop = -420f,
			// Already huge, and joins the Queen mid-fight: stays its size and behind her,
			// so her molten cone can always be read.
			MaxGrow = 1f, BehindOtherBosses = true, TuckedBehindPlatform = true,
			SwingAngle = 0.25f, Lunge = 0f, Breath = 1.2f, CastRaise = 0.3f
		}
		.Joint("body", "", 40, -120)
		.Joint("head", "body", 146, -350)
		.Joint("arm", "body", 220, -290)
		.Joint("off", "body", 30, -240)
		// legs, sinking into the darkness below the platform
		.Part("", Dark, Thick(80f, "-10,0 -36,150 -60,320"))
		.Part("", Dark, Thick(80f, "80,0 92,160 84,320"))
		// the torso, hunched forward over the arena
		.Part("body", Dark,
			"-90,20 -116,-80 -104,-190 -44,-276 56,-334 160,-352 236,-322 256,-250 226,-160 176,-60 150,20")
		.Part("body", TallowDim, Thick(1.5f, "-70,-200 -80,-130 -74,-60"))
		.Part("body", Tallow, Circle(-74f, -55f, 2.6f, 8))
		.Part("body", TallowDim, Thick(1.5f, "20,-300 30,-230 24,-170"))
		.Part("body", Tallow, Circle(24f, -165f, 2.4f, 8))
		// guttered candle stubs along its back, only one still (barely) lit
		.Part("body", Dark2, "-70,-238 -80,-272 -64,-280 -54,-248")
		.Part("body", Dark2, "-26,-286 -34,-324 -16,-330 -8,-294")
		.Part("body", Dark2, "26,-322 22,-352 38,-356 42,-326")
		.Part("body", TallowFlame, "-25,-329 -27,-336 -25,-346 -22,-336")
		// the head, hung forward between the shoulders
		.Part("head", Dark, "92,-318 86,-380 120,-412 176,-414 208,-380 202,-326 160,-302")
		// a heavy brow, sloping down toward the middle into a scowl
		.Part("head", Dark2, "100,-376 146,-362 200,-380 200,-368 146,-350 100,-364")
		// big dim eyes, drooping at the outer corners
		.Part("head", TallowDim, "110,-360 136,-352 134,-342 112,-348")
		.Part("head", TallowDim, "158,-352 186,-362 184,-350 160,-342")
		// a downturned mouth
		.Part("head", Void, "122,-322 148,-332 176,-322 176,-316 148,-325 122,-315")
		// drippings hanging from its chin
		.Part("head", Dark, "110,-312 118,-284 126,-306 138,-276 148,-304 160,-280 172,-308 184,-292 196,-318")
		// near arm: reaching forward and down, its hand resting on the platform
		.Part("arm", Dark, Thick(56f, "220,-290 312,-238 338,-158"), 1)
		.Part("arm", Dark, "298,-160 370,-164 386,-126 370,-96 308,-96 290,-128", 1)
		.Part("arm", TallowDim, Thick(1.3f, "300,-236 310,-206 306,-186"), 1)
		.Part("arm", Tallow, Circle(306f, -182f, 2.2f, 8), 1)
		// far arm, its hand on the platform's near edge
		.Part("off", Dark, Thick(48f, "30,-240 6,-150 80,-64 150,-34"), -1)
		.Part("off", Dark, "118,-56 184,-60 198,-28 184,2 124,2 108,-26", -1);

	// ═════════════════════════════════════════════════════════════════════════
	// RUNE OF NIGHTFALL
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>Grasping Thorns (Rune of Nightfall) → black thorned tendrils coiling up around the ally they strangle, tips cold and pale.</summary>
	public static RigDefinition Thorns() => new RigDefinition
		{
			Id = "thorns", Mode = RigMode.Darkness, Height = 120f, Width = 80f, Scale = 0.46f,
			Breath = 1.6f, InFront = true
		}
		.Joint("body", "", 0, -20)
		.Joint("arm", "body", 10, -60)
		.Joint("off", "body", -12, -50)
		.Part("body", Dark, Thick(7f, "-20,0 -10,-30 -22,-60 -8,-96 -16,-118"))
		.Part("arm", Dark, Thick(6f, "10,0 20,-40 6,-72 22,-104 14,-120"))
		.Part("off", Dark, Thick(5f, "-4,0 -30,-36 -36,-72 -24,-92"))
		.Part("body", Dark2, "-10,-30 -2,-36 -8,-24")
		.Part("body", Dark2, "-22,-60 -32,-66 -20,-54")
		.Part("arm", Dark2, "20,-40 30,-44 22,-34")
		.Part("arm", Dark2, "6,-72 -4,-78 4,-66")
		.Part("off", Dark2, "-30,-36 -40,-34 -32,-28")
		.Part("arm", ColdEye, Circle(14f, -118f, 2.2f, 8))
		.Part("body", ColdEye, Circle(-16f, -116f, 1.8f, 8))
		.Part("off", ColdEye, Circle(-24f, -92f, 1.6f, 8));

	// ═════════════════════════════════════════════════════════════════════════
	// THE ABYSS
	// ═════════════════════════════════════════════════════════════════════════

	static readonly Color AbyssEye = new(0.7f, 0.68f, 0.78f);
	static readonly Color AbyssEyeHalo = new(0.2f, 0.17f, 0.26f);

	/// <summary>
	/// The Infinite Abyss → a towering black mass that rises out of the floor and on past
	/// the top of the screen, two huge pale eyes, and tendrils reaching down from above.
	/// It stands on its origin (see <see cref="RigDefinition.FeetOffset"/>) so the party
	/// fights at its base.
	/// </summary>
	public static RigDefinition TheInfiniteAbyss() => new RigDefinition
		{
			Id = "the-infinite-abyss", Mode = RigMode.Darkness, Height = 720f, Width = 520f, Scale = 0.5f,
			MaxGrow = 1f, FeetOffset = 20f, BurnTop = -340f,
			SwingAngle = 0.55f, Lunge = 0f, Breath = 0.5f, CastRaise = 0.35f,
			Pool = new Vector2(320, 34), PoolColor = new Color(0.01f, 0.006f, 0.012f, 0.95f)
		}
		.Joint("body", "", 0, -200)
		.Joint("head", "body", 0, -150)
		.Joint("arm", "body", 150, -300)
		.Joint("off", "body", -150, -300)
		.Joint("tendrilL", "body", -220, -210)
		.Joint("tendrilR", "body", 220, -230)
		.Joint("tendrilHighL", "body", -60, -430)
		.Joint("tendrilHighR", "body", 90, -460)
		// reaching tendrils, behind the mass
		.Part("tendrilL", Dark, Thick(11f, "-220,-210 -300,-250 -345,-330 -335,-430 -360,-500"), -1)
		.Part("tendrilR", Dark, Thick(11f, "220,-230 300,-300 325,-390 305,-490 330,-560"), -1)
		.Part("tendrilHighL", Dark, Thick(13f, "-60,-430 -150,-520 -130,-640 -175,-730"), -1)
		.Part("tendrilHighR", Dark, Thick(13f, "90,-460 175,-570 150,-700 190,-760"), -1)
		// the mass itself: a ragged base dissolving into the floor, rising out of sight
		.Part("body", Dark,
			"-285,0 -262,-14 -240,4 -214,-12 -186,6 -160,-10 -130,5 -100,-12 -70,4 -40,-10 -10,6 20,-12 " +
			"50,5 80,-10 110,6 140,-12 170,4 200,-12 230,5 258,-12 285,0 " +
			"272,-90 238,-240 214,-400 232,-560 196,-760 -196,-760 -236,-560 -212,-400 -244,-240 -276,-90")
		.Part("body", Void, "-150,-60 -120,-200 -130,-360 -60,-430 60,-440 130,-360 118,-200 150,-60 0,-30")
		// the great tendrils it strikes with, in front
		.Part("arm", Dark, Thick(17f, "150,-300 235,-262 285,-182 302,-92 334,-14"), 1)
		.Part("arm", Dark, "322,-30 352,0 330,6", 1)
		.Part("off", Dark, Thick(17f, "-150,-300 -235,-262 -285,-182 -302,-92 -334,-14"), 1)
		.Part("off", Dark, "-322,-30 -352,0 -330,6", 1)
		// two huge, gloomy, pupil-less eyes: a dim halo around a washed-out pale disc
		.Part("head", AbyssEyeHalo, Circle(-88f, -150f, 48f, 32))
		.Part("head", AbyssEyeHalo, Circle(88f, -150f, 48f, 32))
		.Part("head", AbyssEye, Circle(-88f, -150f, 38f, 32))
		.Part("head", AbyssEye, Circle(88f, -150f, 38f, 32));

	/// <summary>Abyssal Tendril → a single tendril rising from a pool of black, one round pale eye at its tip.</summary>
	public static RigDefinition AbyssalTendril() => new RigDefinition
		{
			Id = "abyssal-tendril", Mode = RigMode.Darkness, Height = 170f, Width = 60f, Scale = 0.42f,
			SwingAngle = 0.9f, Lunge = 6f, Breath = 1.4f,
			Pool = new Vector2(46, 10), PoolColor = new Color(0.01f, 0.006f, 0.012f, 0.95f)
		}
		.Joint("body", "", 0, -40)
		.Joint("arm", "body", 6, -100)
		.Joint("head", "arm", -2, -150)
		.Part("body", Dark, "-22,0 -12,-8 0,2 12,-8 22,0 12,-30 -10,-30")
		.Part("body", Dark, Thick(10f, "0,-24 -10,-60 8,-100"))
		.Part("arm", Dark, Thick(8f, "8,-100 -8,-135 4,-162"))
		.Part("arm", Dark2, "-6,-64 -18,-70 -8,-58")
		.Part("arm", Dark2, "4,-120 16,-126 6,-114")
		.Part("head", AbyssEye, Circle(3f, -162f, 5f, 12));

	// ── helpers ──────────────────────────────────────────────────────────────

	/// <summary>
	/// A thin ring built from short segments (one closed thick polygon would overlap
	/// itself and fail to triangulate).
	/// </summary>
	static RigDefinition Ring(this RigDefinition def, string joint, Color color, float cx, float cy, float r,
		float width, int z = 0, int segments = 36)
	{
		for (var i = 0; i < segments; i++)
		{
			var a0 = Mathf.Tau * i / segments;
			var a1 = Mathf.Tau * (i + 1.15f) / segments; // slight overlap hides seams
			def.Part(joint, color, Thick(width, new[]
			{
				new Vector2(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r),
				new Vector2(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r)
			}), z);
		}
		return def;
	}
}
