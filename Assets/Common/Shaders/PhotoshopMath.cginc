#ifndef PHOTOSHOPMATH_INCLUDED
#define PHOTOSHOPMATH_INCLUDED
/*
** Copyright (c) 2012, Romain Dura romain@shazbits.com
** 
** Permission to use, copy, modify, and/or distribute this software for any 
** purpose with or without fee is hereby granted, provided that the above 
** copyright notice and this permission notice appear in all copies.
** 
** THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES 
** WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF 
** MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY 
** SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES 
** WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN 
** ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR 
** IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

/*
** Photoshop & misc math
** Blending modes, RGB/HSL/Contrast/Desaturate, levels control
**
** Romain Dura | Romz
** Blog: http://mouaif.wordpress.com
** Post: http://mouaif.wordpress.com/?p=94
*/



/*
** Desaturation
*/

float4 Desaturate(float3 color, float Desaturation)
{
	float3 grayXfer = float3(0.3, 0.59, 0.11);
	float d = dot(grayXfer, color);
	float3 gray = float3(d,d,d);
	return float4(lerp(color, gray, Desaturation), 1.0);
}

/*
**HSV
*/


float3 rgb2hsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
 
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}
 
float3 hsv2rgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}



/*
** Hue, saturation, luminance
*/

float3 RGBToHSL(float3 color)
{
	float3 hsl; // init to 0 to avoid warnings ? (and reverse if + remove first part)
	
	float fmin = min(min(color.r, color.g), color.b);    //Min. value of RGB
	float fmax = max(max(color.r, color.g), color.b);    //Max. value of RGB
	float delta = fmax - fmin;             //Delta RGB value

	hsl.z = (fmax + fmin) / 2.0; // Luminance

	if (delta == 0.0)		//This is a gray, no chroma...
	{
		hsl.x = 0.0;	// Hue
		hsl.y = 0.0;	// Saturation
	}
	else                                    //Chromatic data...
	{
		if (hsl.z < 0.5)
			hsl.y = delta / (fmax + fmin); // Saturation
		else
			hsl.y = delta / (2.0 - fmax - fmin); // Saturation
		
		float deltaR = (((fmax - color.r) / 6.0) + (delta / 2.0)) / delta;
		float deltaG = (((fmax - color.g) / 6.0) + (delta / 2.0)) / delta;
		float deltaB = (((fmax - color.b) / 6.0) + (delta / 2.0)) / delta;

		if (color.r == fmax )
			hsl.x = deltaB - deltaG; // Hue
		else if (color.g == fmax)
			hsl.x = (1.0 / 3.0) + deltaR - deltaB; // Hue
		else if (color.b == fmax)
			hsl.x = (2.0 / 3.0) + deltaG - deltaR; // Hue

		if (hsl.x < 0.0)
			hsl.x += 1.0; // Hue
		else if (hsl.x > 1.0)
			hsl.x -= 1.0; // Hue
	}

	return hsl;
}

float HueToRGB(float f1, float f2, float hue)
{
	if (hue < 0.0)
		hue += 1.0;
	else if (hue > 1.0)
		hue -= 1.0;
	float res;
	if ((6.0 * hue) < 1.0)
		res = f1 + (f2 - f1) * 6.0 * hue;
	else if ((2.0 * hue) < 1.0)
		res = f2;
	else if ((3.0 * hue) < 2.0)
		res = f1 + (f2 - f1) * ((2.0 / 3.0) - hue) * 6.0;
	else
		res = f1;
	return res;
}

float3 HSLToRGB(float3 hsl)
{
	float3 rgb;
	
	if (hsl.y == 0.0)
		rgb = float3(hsl.z,hsl.z,hsl.z); // Luminance
	else
	{
		float f2;
		
		if (hsl.z < 0.5)
			f2 = hsl.z * (1.0 + hsl.y);
		else
			f2 = (hsl.z + hsl.y) - (hsl.y * hsl.z);
			
		float f1 = 2.0 * hsl.z - f2;
		
		rgb.r = HueToRGB(f1, f2, hsl.x + (1.0/3.0));
		rgb.g = HueToRGB(f1, f2, hsl.x);
		rgb.b= HueToRGB(f1, f2, hsl.x - (1.0/3.0));
	}
	
	return rgb;
}


/*
** Contrast, saturation, brightness
** Code of this function is from TGM's shader pack
** http://irrlicht.sourceforge.net/phpBB2/viewtopic.php?t=21057
*/

// For all settings: 1.0 = 100% 0.5=50% 1.5 = 150%
float3 ContrastSaturationBrightness(float3 color, float brt, float sat, float con)
{
	// Increase or decrease theese values to adjust r, g and b color channels seperately
	const float AvgLumR = 0.5;
	const float AvgLumG = 0.5;
	const float AvgLumB = 0.5;
	
	const float3 LumCoeff = float3(0.2125, 0.7154, 0.0721);
	
	float3 AvgLumin = float3(AvgLumR, AvgLumG, AvgLumB);
	float3 brtColor = color * brt;
	float d = dot(brtColor, LumCoeff);
	float3 intensity = float3(d,d,d);
	float3 satColor = lerp(intensity, brtColor, sat);
	float3 conColor = lerp(AvgLumin, satColor, con);
	return conColor;
}
		
float3 BlendLighten(float3 baseColor, float3 blendColor){
	return max(blendColor, baseColor);
}
float3 BlendLighten(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendLighten(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendDarken(float3 baseColor, float3 blendColor){
	return min(blendColor, baseColor);
}
float3 BlendDarken(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendDarken(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendLinearBurn(float3 baseColor, float3 blendColor){
	return max(baseColor + blendColor - 1.0, 0.0);
}
float3 BlendLinearBurn(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendLinearBurn(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendLinearDodge(float3 baseColor, float3 blendColor){
	return min(baseColor + blendColor, 1.0);
}
float3 BlendLinearDodge(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendLinearDodge(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendLinearLight(float3 baseColor, float3 blendColor){
	return (blendColor < 0.5) ? BlendLinearBurn(baseColor, (2.0 * blendColor)) : BlendLinearDodge(baseColor, (2.0 * (blendColor - 0.5)));
}
float3 BlendLinearLight(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendLinearLight(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendScreen(float3 baseColor, float3 blendColor){
	return (1.0 - ((1.0 - baseColor) * (1.0 - blendColor)));
}
float3 BlendScreen(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendScreen(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendOverLay(float3 baseColor, float3 blendColor){
	return (baseColor < 0.5) ? (2.0 * baseColor * blendColor) : (1.0 - 2.0 * (1.0 - baseColor) * (1.0 - blendColor));
}
float3 BlendOverLay(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendOverLay(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendSoftLight(float3 baseColor, float3 blendColor){
	return ((blendColor < 0.5) ? (2.0 * baseColor * blendColor + baseColor * baseColor * (1.0 - 2.0 * blendColor)) : (sqrt(baseColor) * (2.0 * blendColor - 1.0) + 2.0 * baseColor * (1.0 - blendColor)));
}
float3 BlendSoftLight(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendSoftLight(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendColorDodge(float3 baseColor, float3 blendColor){
	return (blendColor == 1.0) ? blendColor : min(baseColor / (1.0 - blendColor), 1.0);
}
float3 BlendColorDodge(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendColorDodge(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendColorBurn(float3 baseColor, float3 blendColor){
	return ((blendColor == 0.0) ? blendColor : max((1.0 - ((1.0 - baseColor) / blendColor)), 0.0));
}
float3 BlendColorBurn(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendColorBurn(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendVividLight(float3 baseColor, float3 blendColor){
	return ((blendColor < 0.5) ? BlendColorBurn(baseColor, (2.0 * blendColor)) : BlendColorDodge(baseColor, (2.0 * (blendColor - 0.5))));
}
float3 BlendVividLight(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendVividLight(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendPinLight(float3 baseColor, float3 blendColor){
	return (blendColor < 0.5) ? BlendDarken(baseColor, (2.0 * blendColor)) : BlendLighten(baseColor, (2.0 *(blendColor - 0.5)));
}
float3 BlendPinLight(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendPinLight(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendHardLerp(float3 baseColor, float3 blendColor){
	return ((BlendVividLight(baseColor, blendColor) < 0.5) ? 0.0 : 1.0);
}
float3 BlendHardLerp(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendHardLerp(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendReflect(float3 baseColor, float3 blendColor){
	return ((blendColor == 1.0) ? blendColor : min(baseColor * baseColor / (1.0 - blendColor), 1.0));
}
float3 BlendReflect(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendReflect(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendNegation(float3 baseColor, float3 blendColor){
	return (float3(1,1,1) - abs(float3(1,1,1) - baseColor - blendColor));
}
float3 BlendNegation(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendNegation(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendExclusion(float3 baseColor, float3 blendColor){
	return (baseColor + blendColor - 2.0 * baseColor * blendColor);
}
float3 BlendExclusion(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendExclusion(baseColor, blendColor.rgb),blendColor.a);
}

float3 BlendPhoenix(float3 baseColor, float3 blendColor){
	return (min(baseColor, blendColor) - max(baseColor, blendColor) + float3(1,1,1));
}
float3 BlendPhoenix(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendPhoenix(baseColor, blendColor.rgb),blendColor.a);
}


// Hue Blend mode creates the result color by combining the luminance and saturation of the baseColor color with the hue of the blendColor color.
float3 BlendHue(float3 baseColor, float3 blendColor)
{
	float3 baseHSL = RGBToHSL(baseColor);
	return HSLToRGB(float3(RGBToHSL(blendColor).r, baseHSL.g, baseHSL.b));
}
float3 BlendHue(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendHue(baseColor, blendColor.rgb),blendColor.a);
}


// Saturation Blend mode creates the result color by combining the luminance and hue of the base color with the saturation of the blendColor color.
float3 BlendSaturation(float3 baseColor, float3 blendColor)
{
	float3 baseHSL = RGBToHSL(baseColor);
	return HSLToRGB(float3(baseHSL.r, RGBToHSL(blendColor).g, baseHSL.b));
}
float3 BlendSaturation(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendSaturation(baseColor, blendColor.rgb),blendColor.a);
}


// Color Mode keeps the brightness of the base color and applies both the hue and saturation of the blendColor color.
float3 BlendColor(float3 baseColor, float3 blendColor)
{
	float3 blendHSL = RGBToHSL(blendColor);
	return HSLToRGB(float3(blendHSL.r, blendHSL.g, RGBToHSL(baseColor).b));
}
float3 BlendColor(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendColor(baseColor, blendColor.rgb),blendColor.a);
}


// Luminosity Blend mode creates the result color by combining the hue and saturation of the base color with the luminance of the blend color.
float3 BlendLuminosity(float3 baseColor, float3 blendColor)
{
	float3 baseHSL = RGBToHSL(baseColor);
	return HSLToRGB(float3(baseHSL.r, baseHSL.g, RGBToHSL(blendColor).b));
}
float3 BlendLuminosity(float3 baseColor, float4 blendColor){
	return lerp(baseColor, BlendLuminosity(baseColor, blendColor.rgb),blendColor.a);
}

float3 HSLShift(half3 baseColor, half3 shift){
		half3 hsl = RGBToHSL(baseColor);
		hsl = hsl + shift.xyz;
		hsl.yz = saturate(hsl.yz);
		return HSLToRGB(hsl);
}
float3 HSLShift(float3 baseColor, float4 shift){
	return lerp(baseColor, HSLShift(baseColor, shift.rgb),shift.a);
}


float3 HSVShift(half3 baseColor, half3 shift){
		half3 hsv = rgb2hsv(baseColor);
		hsv = hsv + shift.xyz;
		hsv.yz = saturate(hsv.yz);
		return hsv2rgb(hsv);
}
float3 HSVShift(float3 baseColor, float4 shift){
	return lerp(baseColor, HSVShift(baseColor, shift.rgb),shift.a);
}


/*
** Gamma correction
** Details: http://blog.mouaif.org/2009/01/22/photoshop-gamma-correction-shader/
*/

#define GammaCorrection(color, gamma)								pow(color, 1.0 / gamma)

/*
** Levels control (input (+gamma), output)
** Details: http://blog.mouaif.org/2009/01/28/levels-control-shader/
*/

#define LevelsControlInputRange(color, minInput, maxInput)				min(max(color - float3(minInput,minInput,minInput), float3(0,0,0)) / (float3(maxInput,maxInput,maxInput) - float3(minInput,minInput,minInput)), float3(1,1,1))
#define LevelsControlInput(color, minInput, gamma, maxInput)				GammaCorrection(LevelsControlInputRange(color, minInput, maxInput), gamma)
#define LevelsControlOutputRange(color, minOutput, maxOutput) 			lerp(float3(minOutput,minOutput,minOutput), float3(maxOutput,maxOutput,maxOutput), color)
#define LevelsControl(color, minInput, gamma, maxInput, minOutput, maxOutput) 	LevelsControlOutputRange(LevelsControlInput(color, minInput, gamma, maxInput), minOutput, maxOutput)

#endif