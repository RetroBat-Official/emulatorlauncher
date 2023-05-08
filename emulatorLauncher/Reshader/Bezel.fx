#include "ReShade.fxh"

uniform float2 GameScreen_zoom <
	ui_type = "drag";
	ui_min = 1.00f;
	ui_max = 300.00f;
	ui_step = 0.10f;
	ui_label = "Manual Main Screen Zoom X/Y %";
	ui_category = "User Aspect Settings";
> = float2(100.00, 100.00);

uniform float2 GameScreen_trans <
	ui_type = "drag";
	ui_min = -100.00f;
	ui_max = 100.00f;
	ui_step = 0.10f;
	ui_label = "Manual Main Screen Translation X/Y %";
	ui_category = "User Aspect Settings";
> = float2(0.00, 0.00);

#define GameScreen_scale float2(GameScreen_zoom.x / 100.0f, GameScreen_zoom.y / 100.0f)
#define GameScreen_offset float2(GameScreen_trans.x / 100.0f, GameScreen_trans.y / 100.0f)

texture HudTex	< string source = "#PATH#"; > {Width = #WIDTH#; Height = #HEIGHT#; Format = RGBA8;};
sampler	HudColor 	{ Texture = HudTex; };

float4 PS_Hud(float4 vpos : SV_Position, float2 texcoord : TEXCOORD) : SV_Target {

	float2 uv = texcoord;
	uv = (uv - (0.5f, 0.5f)) / GameScreen_scale + (0.5f, 0.5f) - GameScreen_offset;
	
	float4 hud = tex2D(HudColor, texcoord);		
	return lerp(tex2D(ReShade::BackBuffer, uv), hud, hud.a);
}

technique Bezel {
	pass HudPass {
		VertexShader = PostProcessVS;
		PixelShader = PS_Hud;
	}
}