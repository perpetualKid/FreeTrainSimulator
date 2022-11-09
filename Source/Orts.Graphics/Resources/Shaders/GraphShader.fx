// COPYRIGHT 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

////////////////////////////////////////////////////////////////////////////////
//              D I A G R A M   O V E R L A Y   S H A D E R                   //
////////////////////////////////////////////////////////////////////////////////

////////////////////    G L O B A L   V A L U E S    ///////////////////////////

float2 ScreenSize;
float4 Bounds; // xy = xy position, zw = width/height
float2 GraphSample; // x = index, y = count
float4 BorderColor;
float4 GraphColor;

////////////////////    V E R T E X   I N P U T S    ///////////////////////////

struct VERTEX_INPUT
{
	float4 Position : POSITION;
};

////////////////////    V E R T E X   O U T P U T S    /////////////////////////

struct VERTEX_OUTPUT
{
	float4 Position : POSITION;  // position x, y, z, w
};

////////////////////    V E R T E X   S H A D E R S    /////////////////////////

VERTEX_OUTPUT VSGraph(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;
	
	// The graph is displayed at (Bounds.xy) and is sized (Bounds.zw).
	float x = frac(In.Position.x - GraphSample.x / GraphSample.y);
	Out.Position.x = Bounds.x + (Bounds.z - 2) * x + 1;
	Out.Position.y = - Bounds.y + (Bounds.w - 2)* In.Position.y - Bounds.w + 1;
	Out.Position.xy /= ScreenSize / 2;
	Out.Position.xy -= float2(1, -1);
	Out.Position.w = 1;

	return Out;
}

VERTEX_OUTPUT VSBorder(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;
	
	// The border is displayed at (Bounds.xy) and is sized (Bounds.zw).
	Out.Position.x = Bounds.x + Bounds.z * In.Position.x;
	Out.Position.y = - Bounds.y - Bounds.w * In.Position.y;
	// Viewport adjustment.
	Out.Position.xy /= ScreenSize / 2;
	Out.Position.xy -= float2(1, -1);
	Out.Position.w = 1;

	return Out;
}

////////////////////    P I X E L   S H A D E R S    ///////////////////////////

float4 PSGraph(in VERTEX_OUTPUT In) : COLOR0
{
	return GraphColor;
}

float4 PSBorder(in VERTEX_OUTPUT In) : COLOR0
{
	return BorderColor;
}

////////////////////    T E C H N I Q U E S    /////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// IMPORTANT: ATI graphics cards/drivers do NOT like mixing shader model      //
//            versions within a technique/pass. Always use the same vertex    //
//            and pixel shader versions within each technique/pass.           //
////////////////////////////////////////////////////////////////////////////////

technique Graph {
	pass Pass_0 {
		VertexShader = compile vs_5_0 VSGraph();
		PixelShader = compile ps_5_0 PSGraph();
	}
}

technique Border {
	pass Pass_0 {
		VertexShader = compile vs_5_0 VSBorder();
		PixelShader = compile ps_5_0 PSBorder();
	}
}
