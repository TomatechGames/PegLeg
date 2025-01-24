using Godot;

public partial class FittableTextureRect : TextureRect
{
	public void SetFit(bool value) => StretchMode = value ? StretchModeEnum.KeepAspectCentered : StretchModeEnum.KeepAspectCovered;
}
