using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

public partial class UltimateSpellResource : SpellResource
{
	public virtual bool CanCast(SpellContext ctx)
	{
		return true;
	}
}