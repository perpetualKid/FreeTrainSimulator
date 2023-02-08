////////////////////////////////////////////////////////////////////////////////
//                   P O P U P   W I N D O W   S H A D E R                    //
////////////////////////////////////////////////////////////////////////////////

////////////////////    G L O B A L   V A L U E S    ///////////////////////////

float4x4 World;         // model -> world
float4x4 WorldViewProjection;  // model -> world -> view -> projection
float3   GlassColor;
float    Opacity;
texture  WindowTexture;

sampler WindowSampler = sampler_state
{
	Texture = (WindowTexture);
	MagFilter = Point;
	MinFilter = Point;
	MipFilter = Point;
	AddressU = Clamp;
	AddressV = Clamp;
};

////////////////////    V E R T E X   I N P U T S    ///////////////////////////

struct VERTEX_INPUT
{
	float4 Position     : POSITION;
	float2 TexCoords    : TEXCOORD0;
};

////////////////////    V E R T E X   O U T P U T S    /////////////////////////

struct VERTEX_OUTPUT
{
	float4 Position      : POSITION;
	float4 TexCoords_Pos : TEXCOORD0;
};

////////////////////    V E R T E X   S H A D E R S    /////////////////////////

VERTEX_OUTPUT VSPopupWindow(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;

	Out.Position = mul(In.Position, WorldViewProjection);
	Out.TexCoords_Pos.xy = In.TexCoords;
//	Out.TexCoords_Pos.zw = mul(In.Position, World).xy / ScreenSize;
	return Out;
}

////////////////////    P I X E L   S H A D E R S    ///////////////////////////

float4 PSPopupWindow(in VERTEX_OUTPUT In) : COLOR
{
	float4 Color = tex2D(WindowSampler, In.TexCoords_Pos.xy);
	float Mask = tex2D(WindowSampler, In.TexCoords_Pos.xy + float2(0.5, 0.0)).r;
	float4 ScreenColor = float4(GlassColor, Mask * Opacity);
	return lerp(ScreenColor, Color, Color.a);
}

////////////////////    T E C H N I Q U E S    /////////////////////////////////

technique PopupWindow {
	pass Pass_0 {
		VertexShader = compile vs_5_0 VSPopupWindow();
		PixelShader = compile ps_5_0 PSPopupWindow();
	}
}
