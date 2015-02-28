using UnityEngine;
using System.Collections;

public class PerlinNoise
{
	const int B = 256;
	int[] perm = new int[B+B];
	Texture2D permTex;

	public PerlinNoise(int seed)
	{
		UnityEngine.Random.seed = seed;

		int i, j, k;
		for (i = 0 ; i < B ; i++) 
		{
			perm[i] = i;
		}

		while (--i != 0) 
		{
			k = perm[i];
			j = UnityEngine.Random.Range(0, B);
			perm[i] = perm[j];
			perm[j] = k;
		}
	
		for (i = 0 ; i < B; i++) 
		{
			perm[B + i] = perm[i];
		}
	}
	
	float FADE(float t) { return t * t * t * ( t * ( t * 6.0f - 15.0f ) + 10.0f ); }
	
	int FASTFLOOR(float x) { return ((x)>0) ? ((int)x) : ((int)x-1 ); }
	
	float LERP(float t, float a, float b) { return (a) + (t)*((b)-(a)); }
	
	float GRAD2(int hash, float x, float y)
	{
		int h = hash % 16;
    	float u = h<4 ? x : y;
    	float v = h<4 ? y : x;
		int hn = h % 2;
		int hm = (h/2) % 2;
    	return ((hn != 0) ? -u : u) + ((hm != 0) ? -2.0f*v : 2.0f*v);
	}
	
	public float Noise( float x, float y )
	{
	    int ix0, iy0, ix1, iy1;
	    float fx0, fy0, fx1, fy1, s, t, nx0, nx1, n0, n1;
	
	    ix0 = FASTFLOOR(x); 	// Integer part of x
	    iy0 = FASTFLOOR(y); 	// Integer part of y
	    fx0 = x - ix0;        	// Fractional part of x
	    fy0 = y - iy0;        	// Fractional part of y
	    fx1 = fx0 - 1.0f;
	    fy1 = fy0 - 1.0f;
	    ix1 = (ix0 + 1) & 0xff; // Wrap to 0..255
	    iy1 = (iy0 + 1) & 0xff;
	    ix0 = ix0 & 0xff;
	    iy0 = iy0 & 0xff;
	    
	    t = FADE( fy0 );
	    s = FADE( fx0 );
	
		nx0 = GRAD2(perm[ix0 + perm[iy0]], fx0, fy0);
	    nx1 = GRAD2(perm[ix0 + perm[iy1]], fx0, fy1);
		
	    n0 = LERP( t, nx0, nx1 );
	
	    nx0 = GRAD2(perm[ix1 + perm[iy0]], fx1, fy0);
	    nx1 = GRAD2(perm[ix1 + perm[iy1]], fx1, fy1);
		
	    n1 = LERP(t, nx0, nx1);
	
	    return 0.507f * LERP( s, n0, n1 );
	}
	
	public float FractalNoise(float x, float y, int octNum, float frq, float amp)
	{
		float gain = 1.0f;
		float sum = 0.0f;
		float noise = 0.0f;
	
		for(int i = 0; i < octNum; i++)
		{
			noise = Noise((x*gain) / frq, (y*gain) / frq);
			sum +=  noise * (amp/gain);
			gain *= 2.0f;
		}
		return sum;
	}
	
	public void LoadPermTableIntoTexture()
	{
		permTex = new Texture2D(16, 16, TextureFormat.Alpha8, false);
		permTex.filterMode = FilterMode.Point;
		permTex.wrapMode = TextureWrapMode.Clamp;
		
		for(int i = 0; i < 256; i++)
		{
			int x = i % 16;
			int y = i / 16;

			float v = (float)perm[i] / 255.0f;
				
			permTex.SetPixel(x,y, new Color(v,v,v,v));
		}
		
		permTex.Apply();
	}
	
	public void RenderIntoTexture(Shader shader, RenderTexture renderTex, float octNum, float frq, float amp)
	{
		if(!permTex) LoadPermTableIntoTexture();
		
		Material mat = new Material(shader);
	    mat.SetFloat("_Frq", frq);
	    mat.SetFloat("_Amp", amp);
	    mat.SetFloat("_TexSize", renderTex.width);
	    mat.SetFloat("_Octaves", octNum);
	   
	    Graphics.Blit(permTex, renderTex, mat);
		
	}

}












