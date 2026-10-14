using Godot;
using static healerfantasy.LastLight.RigDefinition;

namespace healerfantasy.LastLight;

/// <summary>
/// Every Last Light character, defined as flat polygons.
///
/// Authoring rules
/// ───────────────
/// • Design space: feet at (0,0), up is -Y, the character faces RIGHT.
/// • Party: greyscale albedo only. 1.0 = cloth/armour that catches the light,
///   ~0.2 = slits and holes. The shader adds the healer's light and the rot.
/// • Darkness: near-black (#0b0a0a-ish). Anything brighter than 50% grey is
///   treated as glowing (eyes, crystal tips, a dead lantern).
/// • Joints: "body" (hips), "head", "arm" (weapon/front arm, swung on attack,
///   raised on cast), "off" (back arm). Extra joints sway in idle.
/// • Higher Z draws in front.
/// </summary>
public static partial class RigLibrary
{
	static readonly Color Dark = new(0.043f, 0.039f, 0.039f);
	static readonly Color Dark2 = new(0.07f, 0.065f, 0.06f);
	static readonly Color Void = new(0f, 0f, 0f);
	static readonly Color ColdEye = new(0.82f, 0.85f, 0.83f);
	static readonly Color ColdGlint = new(0.75f, 0.9f, 1f);

	// ═════════════════════════════════════════════════════════════════════════
	// PARTY
	// ═════════════════════════════════════════════════════════════════════════

	public static RigDefinition Dawnwarden() => new RigDefinition
		{
			Id = "dawnwarden", Mode = RigMode.Party, Height = 182f, Width = 80f, Scale = 0.3f,
			SwingAngle = 1.9f, Lunge = 6f
		}
		.Joint("body", "", 0, -50)
		.Joint("head", "body", 0, -134)
		.Joint("arm", "body", -24, -120)
		.Joint("off", "body", 24, -118)
		// legs
		.Part("", G(0.55f), "-36,0 -32,-60 -8,-60 -8,0")
		.Part("", G(0.55f), "8,0 8,-60 32,-60 38,0")
		// sword arm, behind the body
		.Part("arm", G(0.85f), "-28,-92 -23,-95 -58,-198 -63,-195", -1)
		.Part("arm", G(0.5f), "-38,-104 -16,-112 -14,-105 -36,-97", -1)
		.Part("arm", G(0.7f), "-32,-124 -18,-124 -18,-88 -30,-88", -1)
		// torso + tabard
		.Part("body", G(1f), "-38,-48 -36,-96 -30,-124 -14,-136 14,-136 30,-124 36,-96 40,-48")
		.Part("body", G(0.78f), "-12,-122 12,-122 16,-34 4,-42 -4,-32 -16,-40")
		// great helm
		.Part("head", G(0.92f), "-17,-132 -17,-170 -12,-182 12,-182 17,-170 17,-132")
		.Part("head", G(0.12f), "-10,-160 15,-160 15,-155 -10,-155")
		.Part("head", G(0.12f), "3,-168 7,-168 7,-142 3,-142")
		// kite shield, in front
		.Part("off", G(0.95f), "18,-134 66,-134 66,-62 42,-18 18,-62", 1)
		.Part("off", G(0.55f), "39,-122 45,-122 45,-46 39,-46", 1)
		.Part("off", G(0.55f), "26,-104 58,-104 58,-98 26,-98", 1);

	public static RigDefinition Duskblade() => new RigDefinition
		{
			Id = "duskblade", Mode = RigMode.Party, Height = 118f, Width = 70f, Scale = 0.36f,
			SwingAngle = 1.4f, Lunge = 12f
		}
		.Joint("body", "", 0, -40)
		.Joint("head", "body", 14, -94)
		.Joint("arm", "body", 28, -86)
		.Joint("off", "body", -6, -72)
		.Joint("cape", "body", -6, -88)
		// legs, crouched
		.Part("", G(0.6f), "-26,0 -16,-44 0,-44 -10,0")
		.Part("", G(0.6f), "18,0 14,-44 30,-44 38,0")
		// trailing scarf
		.Part("cape", G(0.7f), "-4,-92 -64,-82 -70,-70 -58,-74 -4,-80", -2)
		// back dagger arm
		.Part("off", G(0.75f), "-4,-76 -26,-56 -21,-50 0,-66", -1)
		.Part("off", G(0.85f), "-24,-58 -54,-30 -20,-50", -1)
		// hunched torso
		.Part("body", G(1f), "-18,-38 -20,-70 -10,-92 6,-100 26,-96 34,-80 30,-38")
		.Part("body", G(0.7f), "-16,-44 30,-44 30,-38 -18,-38")
		// hood with a pointed tail
		.Part("head", G(0.85f), "6,-108 -36,-122 -2,-96", -1)
		.Part("head", G(1f), "0,-94 4,-112 16,-120 30,-114 37,-98 26,-88 8,-86")
		.Part("head", G(0.15f), "21,-108 34,-105 33,-96 22,-95")
		// front dagger arm
		.Part("arm", G(1f), "24,-92 48,-86 48,-79 26,-82", 1)
		.Part("arm", G(0.9f), "46,-88 84,-94 48,-76", 1);

	public static RigDefinition Starcaller() => new RigDefinition
		{
			Id = "starcaller", Mode = RigMode.Party, Height = 178f, Width = 64f, Scale = 0.3f,
			SwingAngle = -0.7f, Lunge = 2f, CastRaise = 1.0f
		}
		.Joint("body", "", 0, -20)
		.Joint("head", "body", 2, -108)
		.Joint("arm", "body", 14, -92)
		.Joint("off", "body", -10, -92)
		.Joint("hat", "head", -6, -150)
		// back arm
		.Part("off", G(0.8f), "-10,-94 -28,-70 -22,-65 -6,-84", -1)
		// robe
		.Part("body", G(1f), "-30,0 -24,-40 -20,-90 -14,-110 14,-110 20,-90 26,-40 34,0")
		.Part("body", G(0.72f), "-4,-104 6,-104 11,0 -9,0")
		// hat (bent tip sways on its own joint) + beard
		.Part("head", G(0.95f), "-36,-104 38,-104 24,-115 -22,-115")
		.Part("head", G(1f), "-16,-114 18,-114 12,-134 0,-148 -14,-138")
		.Part("hat", G(1f), "-14,-138 0,-148 -8,-160 -34,-180 -20,-160")
		.Part("head", G(0.85f), "-2,-104 16,-104 6,-72")
		.Part("head", G(0.15f), "2,-103 14,-103 12,-99 3,-99")
		// staff arm, in front
		.Part("arm", G(1f), "10,-94 40,-100 40,-90 12,-82", 1)
		.Part("arm", G(0.7f), Thick(5f, "38,-160 40,-80 42,0"), 1)
		.Part("arm", G(1f), Circle(38f, -168f, 9f), 1);

	public static RigDefinition Healer() => new RigDefinition
		{
			Id = "healer", Mode = RigMode.Light, Height = 176f, Width = 60f, Scale = 0.26f,
			SwingAngle = -0.6f, CastRaise = 1.1f, LightJoint = "flame", LightOffset = new Vector2(3, -20)
		}
		.Joint("body", "", 0, -30)
		.Joint("head", "body", 2, -120)
		.Joint("arm", "body", 14, -98)
		.Joint("off", "body", -20, -98)
		.Joint("flame", "head", 3, -116)
		// crook staff in the back hand
		.Part("off", new Color("f5dfa8"), Thick(4f, "-34,4 -36,-150 -33,-166 -24,-173 -15,-168 -12,-160"), -1)
		.Part("off", new Color("eedbb0"), "-20,-102 -38,-98 -36,-89 -18,-91", -1)
		// robe
		.Part("body", new Color("fff3d6"),
			"-30,4 -26,-50 -22,-90 -20,-112 -16,-130 -6,-140 10,-140 20,-130 22,-112 24,-90 28,-50 34,4")
		// hood opening
		.Part("head", new Color("c99a58"), "-8,-110 -6,-124 4,-130 12,-124 14,-110 4,-106")
		// the flame where a face should be
		.Part("flame", new Color("ffe7b0"), "3,-178 12,-163 18,-147 16,-130 3,-112 -10,-130 -12,-147 -6,-163")
		.Part("flame", new Color("ffffff"), "3,-157 9,-143 10,-128 3,-118 -4,-128 -5,-143")
		// reaching arm
		.Part("arm", new Color("f2deb0"), "14,-98 50,-114 52,-106 18,-88", 1)
		.Part("arm", new Color("ffffff"), Circle(54f, -110f, 6f, 10), 1);

	/// <summary>
	/// The camp merchant: a hunched peddler under a mountain of goods, leaning on a
	/// stick with a small lantern of its own, which lights the figure.
	/// </summary>
	public static RigDefinition Merchant() => new RigDefinition
		{
			Id = "merchant", Mode = RigMode.Party, Height = 175f, Width = 110f, Scale = 0.38f, Breath = 0.7f,
			LightJoint = "lantern", LightOffset = new Vector2(4f, 26f), OwnLightRadius = 0.3f
		}
		.Joint("body", "", 0, -40)
		.Joint("head", "body", 24, -118)
		.Joint("arm", "body", 28, -104)
		.Joint("burden", "body", -30, -100)
		.Joint("lantern", "arm", 62, -146)
		// the pack, piled high behind
		.Part("burden", G(0.62f), "-72,-36 -78,-116 -64,-148 -22,-154 -6,-128 -8,-40", -1)
		.Part("burden", G(0.42f), "-76,-90 -8,-94 -8,-86 -76,-82", -1)
		.Part("burden", G(0.5f), Circle(-40f, -162f, 15f), -2)
		.Part("burden", G(0.5f), Circle(-62f, -158f, 11f), -2)
		.Part("burden", G(0.55f), Thick(4f, "-58,-146 -86,-196"), -2)
		.Part("burden", G(0.55f), "-24,-170 -6,-170 -4,-150 -26,-150", -2)
		// hunched robe
		.Part("body", G(1f), "-28,0 -30,-62 -22,-102 -4,-124 18,-128 34,-112 36,-78 30,-40 34,0")
		.Part("body", G(0.7f), "-30,-60 36,-64 36,-56 -30,-52")
		.Part("body", G(0.8f), "-22,-26 -8,-36 6,-24 -10,-12")
		// hood, leaning forward
		.Part("head", G(0.95f), "8,-118 12,-144 30,-158 48,-152 56,-132 48,-114 26,-108")
		.Part("head", G(0.15f), "38,-142 52,-136 50,-122 38,-122")
		// reaching arm and walking stick, in front
		.Part("arm", G(0.9f), "26,-110 52,-98 54,-89 28,-98", 1)
		.Part("arm", G(0.7f), Thick(5f, "54,-150 56,-80 58,0"), 1)
		// lantern hanging off the stick
		.Part("lantern", G(0.6f), Thick(1.5f, "56,-148 64,-146 66,-132"), 1)
		.Part("lantern", G(0.75f), "58,-132 74,-132 76,-108 56,-108", 1)
		.Part("lantern", new Color("e9b56a"), "61,-128 71,-128 72,-113 60,-113", 1)
		.Part("lantern", G(0.75f), "60,-108 72,-108 66,-102", 1);

	// ═════════════════════════════════════════════════════════════════════════
	// THE ANCIENT KEEP — darkness
	// ═════════════════════════════════════════════════════════════════════════

	/// <summary>Crystal Knight → a black knight with obsidian shards growing out of it.</summary>
	public static RigDefinition CrystalKnight() => new RigDefinition
		{
			Id = "crystal-knight", Mode = RigMode.Darkness, Height = 320f, Width = 170f, Scale = 0.36f,
			SwingAngle = 1.5f, Lunge = 10f, Breath = 0.6f, CastRaise = 1.6f, Pool = new Vector2(150, 26)
		}
		.Joint("body", "", 0, -90)
		.Joint("head", "body", 20, -226)
		.Joint("arm", "body", 52, -206)
		.Joint("off", "body", -44, -204)
		.Joint("shards", "body", -40, -220)
		// legs
		.Part("", Dark, "-70,0 -60,-92 -10,-92 -18,0")
		.Part("", Dark, "14,0 10,-92 60,-92 80,0")
		// back arm
		.Part("off", Dark, "-40,-228 -78,-216 -90,-168 -68,-148 -50,-196", -1)
		// shards growing from the back (they shiver on their own joint)
		.Part("shards", Dark2, "-62,-218 -98,-304 -48,-232", -2)
		.Part("shards", Dark2, "-36,-228 -48,-334 -14,-234", -2)
		.Part("shards", Dark2, "-74,-180 -134,-244 -78,-170", -2)
		.Part("shards", Dark2, "58,-226 72,-292 86,-214", -2)
		.Part("shards", ColdGlint, "-93,-292 -98,-304 -87,-290", -2)
		.Part("shards", ColdGlint, "-46,-318 -48,-334 -42,-317", -2)
		.Part("shards", ColdGlint, "-125,-236 -134,-244 -121,-239", -2)
		.Part("shards", ColdGlint, "71,-280 72,-292 76,-279", -2)
		// torso + ragged tabard
		.Part("body", Dark, "-82,-88 -86,-160 -72,-212 -30,-234 40,-234 78,-214 88,-160 82,-88")
		.Part("body", Dark2, "-30,-122 38,-122 46,12 32,-4 20,14 8,-2 -6,14 -18,-2 -30,12 -40,-4")
		// helm, crest shard, visor glow
		.Part("head", Dark, "-6,-224 -6,-268 4,-282 36,-282 46,-268 46,-224")
		.Part("head", Dark2, "12,-280 22,-322 32,-280")
		.Part("head", ColdGlint, "20,-312 22,-322 25,-311")
		.Part("head", new Color(0.66f, 0.86f, 1f), "8,-255 45,-255 45,-249 8,-249")
		// sword arm + greatsword, point resting forward-down
		.Part("arm", Dark, "36,-234 76,-228 84,-196 60,-186 40,-200", 1)
		.Part("arm", Dark, "58,-196 76,-140 62,-132 46,-186", 1)
		.Part("arm", Dark2, "62,-150 76,-158 156,18 140,26", 1)
		.Part("arm", Dark, "50,-140 86,-160 90,-151 54,-131", 1);

	/// <summary>Bringer of Death → a hunched pilgrim with dead candles, a scythe and a dead lantern.</summary>
	public static RigDefinition BringerOfDeath() => new RigDefinition
		{
			Id = "bringer-of-death", Mode = RigMode.Darkness, Height = 340f, Width = 230f, Scale = 0.36f,
			SwingAngle = 1.3f, Lunge = 14f, Breath = 0.8f, CastRaise = 1.4f, Pool = new Vector2(170, 28)
		}
		.Joint("body", "", 0, -80)
		.Joint("head", "body", 60, -228)
		.Joint("arm", "body", 70, -192)
		.Joint("off", "body", -30, -190)
		.Joint("burden", "body", -60, -200)
		.Joint("lantern", "off", -58, -134)
		// dead candles + bundle on the back
		.Part("burden", Dark, "-121,-296 -113,-296 -113,-256 -121,-256", -3)
		.Part("burden", Dark, "-97,-322 -88,-322 -88,-276 -97,-276", -3)
		.Part("burden", Dark, "-71,-332 -63,-332 -63,-290 -71,-290", -3)
		.Part("burden", Dark, "-45,-326 -36,-326 -36,-280 -45,-280", -3)
		.Part("burden", Dark, "-21,-308 -13,-308 -13,-272 -21,-272", -3)
		.Part("burden", new Color(0.2f, 0.19f, 0.18f), Thick(2.5f, "-93,-324 -100,-344 -90,-360 -98,-382"), -3)
		.Part("burden", new Color(0.2f, 0.19f, 0.18f), Thick(2.5f, "-40,-328 -32,-348 -44,-366 -36,-390"), -3)
		.Part("burden", Dark2, "-132,-180 -152,-232 -132,-290 -72,-318 -12,-306 18,-266 -10,-200 -62,-170", -2)
		// back arm holding a dead lantern
		.Part("off", Dark, "-30,-194 -62,-142 -52,-130 -20,-172", -1)
		.Part("lantern", Dark, Thick(1.5f, "-58,-134 -58,-112"), -1)
		.Part("lantern", Dark, "-66,-112 -50,-112 -45,-97 -50,-82 -66,-82 -71,-97", -1)
		.Part("lantern", new Color(0.52f, 0.58f, 0.57f), "-62,-106 -54,-106 -51,-97 -54,-88 -62,-88 -65,-97", -1)
		// robe with a tattered hem
		.Part("body", Dark,
			"-110,0 -100,-60 -90,-140 -80,-200 -50,-250 0,-270 50,-262 80,-240 96,-200 100,-150 110,-80 122,0 " +
			"108,14 96,0 84,16 70,0 56,18 40,0 26,16 10,0 -6,18 -22,0 -38,16 -54,0 -70,14 -86,0 -100,12")
		// hood, void, cold eyes
		.Part("head", Dark, "30,-252 70,-266 102,-254 120,-224 122,-196 104,-188 80,-198 50,-214")
		.Part("head", Dark, "40,-254 10,-284 -34,-292 2,-268 30,-258")
		.Part("head", Void, "88,-228 117,-220 117,-197 93,-199")
		.Part("head", ColdEye, Circle(104f, -213f, 3f, 8))
		.Part("head", ColdEye, Circle(112f, -217f, 2.2f, 8))
		.Part("head", ColdEye, Circle(108f, -204f, 1.6f, 8))
		// scythe arm
		.Part("arm", Dark, "60,-198 122,-152 130,-134 110,-128 54,-172", 1)
		.Part("arm", Dark2, Thick(7f, "96,-40 122,-140 152,-252"), 1)
		.Part("arm", Dark, "152,-254 118,-272 56,-268 4,-240 66,-252 118,-248 148,-240", 1);

	/// <summary>Demon Slime → The Hunger: a heaving mound of dark with a horned skull and one great claw.</summary>
	public static RigDefinition DemonSlime() => new RigDefinition
		{
			Id = "demon-slime", Mode = RigMode.Darkness, Height = 280f, Width = 380f, Scale = 0.37f,
			SwingAngle = 1.1f, Lunge = 16f, Breath = 2.2f, CastRaise = 0.8f, Pool = new Vector2(230, 30)
		}
		.Joint("body", "", 0, -10)
		.Joint("head", "body", 70, -160)
		.Joint("arm", "body", 100, -150)
		.Joint("off", "body", -90, -150)
		// tendrils on the back
		.Part("off", Dark, Thick(12f, "-80,-150 -122,-214 -100,-270 -142,-318"), -2)
		.Part("off", Dark, Thick(9f, "-110,-120 -168,-168 -170,-226 -212,-250"), -2)
		.Part("off", Dark, Thick(8f, "-40,-190 -56,-252 -30,-300 -50,-336"), -2)
		// the mound
		.Part("body", Dark,
			"-200,0 -190,-60 -160,-130 -110,-190 -40,-222 30,-216 90,-190 130,-140 160,-80 192,0 " +
			"170,12 150,0 130,16 110,0 90,14 60,0 40,18 10,0 -20,16 -50,0 -80,14 -110,0 -140,16 -170,0 -186,10")
		// eyes scattered over the body
		.Part("body", ColdEye, Circle(-120f, -140f, 3f, 8))
		.Part("body", ColdEye, Circle(-88f, -172f, 2.2f, 8))
		.Part("body", ColdEye, Circle(-60f, -118f, 2.6f, 8))
		.Part("body", ColdEye, Circle(-142f, -88f, 1.8f, 8))
		.Part("body", ColdEye, Circle(14f, -196f, 2.4f, 8))
		.Part("body", ColdEye, Circle(-30f, -182f, 1.8f, 8))
		// horned skull
		.Part("head", Dark, "50,-208 18,-276 68,-216")
		.Part("head", Dark, "102,-216 146,-270 116,-200")
		.Part("head", Dark2, "30,-150 40,-200 80,-222 120,-202 132,-160 116,-130 90,-120 60,-126")
		.Part("head", new Color(0.88f, 0.92f, 0.9f), "70,-180 85,-176 81,-165 68,-169")
		.Part("head", new Color(0.88f, 0.92f, 0.9f), "97,-177 111,-179 109,-167 97,-166")
		.Part("head", Void, "66,-148 112,-148 106,-130 99,-141 93,-128 87,-141 80,-128 73,-141")
		// the great claw
		.Part("arm", Dark, "84,-164 172,-204 202,-182 122,-128", 1)
		.Part("arm", Dark, "190,-204 254,-226 202,-188", 1)
		.Part("arm", Dark, "198,-190 262,-180 202,-176", 1)
		.Part("arm", Dark, "194,-178 244,-142 190,-166", 1);
}
