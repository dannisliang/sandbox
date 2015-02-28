using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // used for Sum of array

public class Terrain_holistic : MonoBehaviour {

	// PUBLIC ACCESSORS
	public bool debugLogs = false;
	public bool resizeOnStart = false;
	public float newX = 1000, newY = 1000, newZ = 1000;
	public bool flattenOnStart = true;
	public float flattenedHeight = 0;
	public bool newPerlin = false;
	public float perlinSmoothness = 15.0f;	// how smooth is the Perlin modifier (1.0 for strongest)
	public bool humanActions = true;
	public float pullStrength = 0.001f;  	// relativizating factor for a normalized strenght pull
	public float pullRadius = 10;			// in world units
	public float wheelIncrement = 0.5f;		// pullRadius increment on mousewheel scroll
	public enum pullFallOffType { Flat = 0, Linear = 1, Sphere = 2, Needle = 3, Gauss = 4, Cosine = 5 };
	public pullFallOffType fallOff = pullFallOffType.Cosine;
	public enum ET { steepness = 1, gradientDescent = 2, wind = 3  };
	public ET ErosionType = ET.steepness;
	public bool updateTexturesOnGeo = false;		// update texture blending on geomTime?
	public float rockSlope = 30.0f;
	public bool updateBasinsOnGeo = true;
	public bool riverErosionOnGeo = true;
	public bool riverGizmos = false;

	// BASE VARS
	// Terrain
	protected Terrain thisTerrain;  		// the terrain
	private Vector3 size;					// terrain size in world units
	private int xRes, zRes;					// number of pixels in each direction
	private float xPixSize, zPixSize;		// pixel size in world coordinates (e.g. size.x / xRes)
	private float[,] heights;				// keeps track of current terrain heights
	public float[,,] splatmapData; 

	// Dynamic objects
	private List<MountainSeed> mountainSpots = new List<MountainSeed>();
	private List<WaterSeed> waterSpots = new List<WaterSeed>();

	// Other Game Objects
	private GameObject FPC;					// First Person Controller
	private CharacterController FPCCont;	// the controller object
	private Camera FPCCamera;				// the FPC camera
	private MouseLook FPCCameraScript;
	private Camera orbitCamera;				// the god-like orbiting camera
	private maxCamera orbitCameraScript;
	private GameObject dirLightObj;			// main directional light object [debug] 
	private Light dirLight;					// light component in object
	private Projector cursorProjector;		// a projector to use as terrain cursor: http://docs.unity3d.com/Manual/class-Projector.html

	// State
	private int frame = 0; 					// current frame
	private bool geoTimeScale = false;		// are we on Geological Time Scale ?
	private int year = 0;					// fictional year measure
	private Vector3 prevPosition;			// buffer human position before going into god mode

	// Actions

	// UI
	private const int TERRAFORM = 0, MOUNTAIN = 1, WATER = 2;
	private int cursorMode = TERRAFORM;
	private GUIContent labelContent = new GUIContent("0");

	
	
//	███████╗████████╗ █████╗ ██████╗ ████████╗
//	██╔════╝╚══██╔══╝██╔══██╗██╔══██╗╚══██╔══╝
//	███████╗   ██║   ███████║██████╔╝   ██║   
//	╚════██║   ██║   ██╔══██║██╔══██╗   ██║   
//	███████║   ██║   ██║  ██║██║  ██║   ██║   
//	╚══════╝   ╚═╝   ╚═╝  ╚═╝╚═╝  ╚═╝   ╚═╝   			
	void Start () {
		// Initialize Terrain vars
		thisTerrain = Terrain.activeTerrain;
		size = thisTerrain.terrainData.size;
		xRes = thisTerrain.terrainData.heightmapWidth;
		zRes = thisTerrain.terrainData.heightmapHeight;
		xPixSize = size.x / xRes;
		zPixSize = size.z / zRes;
		heights = thisTerrain.terrainData.GetHeights (0, 0, xRes, zRes);

		// Initialize other vars
		FPC = GameObject.Find ("First Person Controller");
		FPCCont = FPC.GetComponent<CharacterController> ();
		FPCCamera = GameObject.Find ("FPCamera").GetComponent<Camera>();
		FPCCameraScript = GameObject.Find ("FPCamera").GetComponent<MouseLook>();
//		FPCCameraScript.enabled = true;
		orbitCamera = GameObject.Find ("GodCamera").GetComponent<Camera>();
		orbitCameraScript = GameObject.Find ("GodCamera").GetComponent<maxCamera> ();
//		orbitCameraScript.enabled = false;

		dirLightObj = GameObject.Find ("base_sun");
		dirLight = dirLightObj.GetComponent<Light> ();
		cursorProjector = GetComponentInChildren<Projector> ();
		cursorProjector.orthographic = true;
		cursorProjector.orthographicSize = pullRadius;

		// Refit terrain under certain flags
		if (resizeOnStart) {
			resizeTerrain(newX, newY, newX);
		}
		if (flattenOnStart) {
			flattenTerrain(flattenedHeight);
		}
		if (newPerlin) {
			perlinTerrain();
		}

		// Apply new Textures based on terrain topology
		splatmapData = new float[thisTerrain.terrainData.alphamapWidth, thisTerrain.terrainData.alphamapHeight, thisTerrain.terrainData.alphamapLayers];
		clearAlphaMaps (); // Set Alpha Map to solid black
		applyTexture ();  // Apply Alpha Map according to start configuration

		// Set the FPC's position to sit on top of Terrain's center
		float h = thisTerrain.terrainData.GetInterpolatedHeight(0.5f, 0.5f);
		Vector3 terrCenter = new Vector3 (size.x / 2, h + FPCCont.height / 2 + 1, size.z / 2);
		FPC.transform.position = terrCenter;

		// TEST: FIRE WATER BASINS ON A GRID
//		for (int i = 100; i < 1000; i += 100){
//			for (int j = 100; j < 1000; j += 100){
//				Vector3 p = new Vector3(i, 
//				            			thisTerrain.terrainData.GetInterpolatedHeight( (float) (i) / 1000, (float) (j) / 1000 ),
//				                        j);
//				WaterSeed ws = new WaterSeed(this, p, pullRadius);
//				waterSpots.Add(ws);
//			}
//		}

	}

	
	
//	██╗   ██╗██████╗ ██████╗  █████╗ ████████╗███████╗
//	██║   ██║██╔══██╗██╔══██╗██╔══██╗╚══██╔══╝██╔════╝
//	██║   ██║██████╔╝██║  ██║███████║   ██║   █████╗  
//	██║   ██║██╔═══╝ ██║  ██║██╔══██║   ██║   ██╔══╝  
//	╚██████╔╝██║     ██████╔╝██║  ██║   ██║   ███████╗
//	 ╚═════╝ ╚═╝     ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚══════╝
	// Update is called once per frame
	void Update () {

		// Pre-update state-based vars
		frame++;

		// KEYBOARD INTERACTION
		if (Input.GetKeyDown (KeyCode.G)) {
			riverGizmos = !riverGizmos;
		}
		// Toggle geological time scale on right mouse click
		if (Input.GetKeyDown(KeyCode.T)) {
			toggleGeoTime();
			if (geoTimeScale) {
				storeCurrentPosition();
				enableCursor(false);
			} else {
				resetPrevPosition();
				enableCursor(true);
			}
			toggleMainCamera();
		}
		// Change to Terraform mode
		if (Input.GetKeyDown (KeyCode.P)) {
			cursorMode = TERRAFORM;
		}
		// Change to Mountain mode
		if (Input.GetKeyDown (KeyCode.M)) {
			cursorMode = MOUNTAIN;
		}
		// Change to Water mode
		if (Input.GetKeyDown (KeyCode.F)) {
			cursorMode = WATER;
		}
		// Force Erode
		if (Input.GetKey(KeyCode.E)){
			switch(ErosionType) {

			case(ET.steepness):
				SteepnessErode();
				break;
				
			case(ET.wind):
				WindErode();
				break;
				
			case(ET.gradientDescent):
				GradientErode();
				break;
			}
		}
		// Recompute textures on ErodeUP
		if (Input.GetKeyUp(KeyCode.E)) {
			applyTexture ();
		}

		// MOUSE INTERACTIONS
		// Perform human actions if applicable (disabled for geotime mode)
		if (humanActions && !geoTimeScale) {

			// Calculate terrain cursor position
			RaycastHit hit;
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			bool wasHit = Physics.Raycast(ray, out hit);

			Vector3 targetPos = new Vector3(hit.point.x, hit.point.y + 5f, hit.point.z);

			cursorProjector.transform.position = targetPos;

			// MOUSE BUTTON DOWN
			if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) {
				switch(cursorMode) {
				// set mountain seeds
				case MOUNTAIN:
					MountainSeed ms = new MountainSeed(this, hit.point, pullRadius);
					if (wasHit) mountainSpots.Add(ms);
					if (debugLogs) Debug.Log(ms.ToString());
					break;
				case WATER:
					WaterSeed ws = new WaterSeed(this, hit.point, pullRadius);
					if (wasHit) waterSpots.Add(ws);
					if (debugLogs) Debug.Log(ws.ToString());
					break;
				}
			}

			// MOUSE BUTTON HELD DOWN
			// If any mouse button is pressed, perform actions based on cursorMode
			if (Input.GetMouseButton(0) || Input.GetMouseButton (1)) {
				switch(cursorMode) {
				// push/pull terrain
				case TERRAFORM:
					if(wasHit) {
						humanPullTerrain(hit.point, Input.GetMouseButton(0)); 
					}
					break;
				}
			}
			// Apply textures on human terraforming mouse up
			if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp (1)) {
				switch(cursorMode) {
					// push/pull terrain
				case TERRAFORM:
					if(wasHit) {
						//applyTexture();
					}
					break;
				}
			}


			// If scrollwheel, update pullRadius and cursorProjector
			float wheel = Input.GetAxis("Mouse ScrollWheel");
			if (wheel != 0) {
				//float dr = wheel > 0 ? wheelIncrement : -wheelIncrement;
				//pullRadius += dr;
				pullRadius *= wheel > 0 ? wheelIncrement : ( 1/ wheelIncrement);
				cursorProjector.orthographicSize = pullRadius;
			}

		}


		// If on geoTimeScale, what to do
		if (geoTimeScale) {
			year += 1000;

			// Update GUI elements
			labelContent.text = "Year " + year.ToString() + "...";

			// call the main function with terrain modifications for this mode
			geoTimeTerrainMod();
		}

		if (Input.GetKeyDown (KeyCode.R)) {
			resetTerrain();
		}


	}


	/**
	 * Called everyframe, for UI inputs
	 */
	void OnGUI() {
		if (geoTimeScale) GUI.Label (new Rect (25, 50, 200, 30), labelContent);
	}

	/**
	 * DEBUG GIZMO DISPLAY
	 **/
	void OnDrawGizmos() {
		if (riverGizmos) {
			Gizmos.color = Color.blue;
			for (int i = 0; i < waterSpots.Count; i++) {
				WaterSeed ws = waterSpots[i];
				for (int j = 0; j < ws.basinVertices.Count; j++) {
					BasinVertex bv = ws.basinVertices[j];
					Gizmos.DrawLine (bv.position, bv.position + 10 * bv.normal);
				}
			}
		}
	}







	
	
//	███████╗██╗   ██╗███╗   ██╗ ██████╗███████╗
//	██╔════╝██║   ██║████╗  ██║██╔════╝██╔════╝
//	█████╗  ██║   ██║██╔██╗ ██║██║     ███████╗
//	██╔══╝  ██║   ██║██║╚██╗██║██║     ╚════██║
//	██║     ╚██████╔╝██║ ╚████║╚██████╗███████║
//	╚═╝      ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝╚══════╝

	/**
	 * 
	 */
	private void toggleGeoTime() 
	{
		geoTimeScale = !geoTimeScale;

		// placeholder for visual change on geotime
		dirLight.intensity = geoTimeScale ? 0.4f : 0.5f;

		if (!geoTimeScale) {
			applyTexture ();
			SteepnessErode();
		}
	}

	/**
	 * This is the main function called when on geoTimeScale. 
	 * Add here the list of things that should happen while on this mode
	 */
	private void geoTimeTerrainMod() {
		for (int i = 0; i < mountainSpots.Count; i++) {
			MountainSeed sp = mountainSpots[i];
			sp.applyToTerrain();
		}

		if (updateTexturesOnGeo) {
			applyTexture ();
		}

		if (updateBasinsOnGeo) {
			for (int l = waterSpots.Count, i = 0; i < l; i++) {
				waterSpots[i].updateHeight();
			}
		}

		if (riverErosionOnGeo) {
			RiverErode();
		}

	}


	/**
	 * Sets a new size for the passed terrain
	 */
	private void resizeTerrain (float newX, float newY, float newZ)
	{
		Vector3 s = new Vector3 (newX, newY, newZ);
		thisTerrain.terrainData.size = s;
	}

	/**
	 * Sets all the height of the terrain to the same target normalized height
	 */
	private void flattenTerrain (float newRelHeight)
	{
		for (int i = 0; i < xRes; i++) {
			for (int j = 0; j < zRes; j++) {
				heights[i, j] = newRelHeight;
			}
		}
		thisTerrain.terrainData.SetHeights (0, 0, heights);
	}
	
	/**
	 * Given the RaycastHit object, and a pull/push flag, performs pull/push 
	 * modifications at the human scale in the terrain
	 */
	//private void humanPullTerrain(RaycastHit hit, bool isPull)
	private void humanPullTerrain(Vector3 point, bool isPull)
	{
		// world coordinates of hit center
		float x = point.x;
		float z = point.z;

		// set the domain limits for vertices this pull will affect
		int u0, uCount, v0, vCount;
		getPixelLimits (point, pullRadius, out u0, out v0, out uCount, out vCount);

		// compute pull/push
		float[,] pullPatch = thisTerrain.terrainData.GetHeights (u0, v0, uCount, vCount);   // WARNING: pullPatch has dimensions float[vCount, uCount] !
		Vector2 pHit2 = new Vector2 (x, z);
		for (int i = 0; i < uCount ; i++) {
			for (int j = 0; j < vCount; j++) {
				Vector2 p0 = new Vector2((u0 + i) * xPixSize, (v0 + j) * zPixSize);
				float dist = Vector2.Distance(p0, pHit2);
				if (dist < pullRadius) {
					float delta = 0.0f;

					switch(fallOff) {
					case(pullFallOffType.Linear):
						delta = pullStrength * (1 - dist / pullRadius);
						break;

					case(pullFallOffType.Sphere):
						delta = pullStrength * Mathf.Sqrt(pullRadius * pullRadius - dist * dist) / pullRadius;
						break;

					case(pullFallOffType.Needle):
						delta = pullStrength * (1 - dist * dist / pullRadius / pullRadius);
						break;

					case(pullFallOffType.Gauss):
						//return Mathf.Clamp01 (Mathf.Pow (360.0, -Mathf.Pow (distance / inRadius, 2.5) - 0.01));
						delta = pullStrength * Mathf.Clamp01(Mathf.Pow(360.0f, -Mathf.Pow(dist / pullRadius, 2.5f) - 0.01f));
						break;

					case(pullFallOffType.Cosine):
						delta = pullStrength * ( Mathf.Cos(dist * Mathf.PI / pullRadius) + 1 ) / 2;
						break;

					case(pullFallOffType.Flat):
					default:
						delta = pullStrength;
						break;
					}

					pullPatch[j, i] += isPull ? delta : -delta;		// WARNING: pullPatch has dimensions float[vCount, uCount] !
				}
			}
		}
		thisTerrain.terrainData.SetHeights (u0, v0, pullPatch);
	}


	private bool getPixelLimits (Vector3 location, float radius, out int minU, out int minV, out int uCount, out int vCount)
	{
		// is point inside terrain? (assuming terrain origin is on 0,0)
		if (location.x < 0 || location.x > size.x || location.z < 0 && location.z > size.z) {
			minU = uCount = minV = vCount = 0;
			return false;
		}
		minU = Mathf.CeilToInt ((location.x - radius) / xPixSize);
		minV = Mathf.CeilToInt ((location.z - radius) / zPixSize);
		if (minU < 0) minU = 0;
		if (minV < 0) minV = 0;

		int maxU = Mathf.FloorToInt ((location.x + radius) / xPixSize);
		int maxV = Mathf.FloorToInt ((location.z + radius) / zPixSize);
		if (maxU > xRes) maxU = xRes;
		if (maxV > zRes) maxV = zRes;

		uCount = maxU - minU + 1;
		vCount = maxV - minV + 1;

		return true;

	}

	private void perlinTerrain()
	{
		Random.seed = System.DateTime.Now.Millisecond;
		float off = Random.Range(0.0f,100.0f);
		float tileSize = 3.0f;
		float[,] perlinHeights = new float[thisTerrain.terrainData.heightmapWidth, thisTerrain.terrainData.heightmapHeight];
		
		for (int i = 0; i < thisTerrain.terrainData.heightmapWidth; i++){
			for (int j = 0; j < thisTerrain.terrainData.heightmapHeight; j++){
				perlinHeights[i, j] = heights[i, j] + Mathf.PerlinNoise((((float)i / (float)thisTerrain.terrainData.heightmapWidth) * tileSize)+off, (((float)j / (float)thisTerrain.terrainData.heightmapHeight) * tileSize)+off)/perlinSmoothness;
			}
		}

		heights = perlinHeights;
		thisTerrain.terrainData.SetHeights(0, 0, perlinHeights);
	}

	
	private void GradientErode ()
	{
		float[,] arrHeights = thisTerrain.terrainData.GetHeights (0, 0, xRes, zRes);   // WARNING: pullPatch has dimensions float[vCount, uCount] !
		
		//int x = (int) Random.Range (0, xRes-1);
		//int z = (int) Random.Range (0, zRes-1);
		
		for (int i = 0; i < xRes; i++) { //xRes
			for (int j = 0; j < zRes; j++) { //zRes
				
				Vector3 nv = thisTerrain.terrainData.GetInterpolatedNormal((float)(i)/xRes,(float)(j)/zRes); // get the terrain normal for vertex
				//Vector3 dv = Vector3.Cross (nv, Vector3.up); // find the cross product of the normal and world up vector
				nv.Normalize ();
				//Vector3 rv = Quaternion.AngleAxis (90, nv) * dv;
				arrHeights[j, i] -= nv.y;		
			}
		}
		heights = arrHeights;
		thisTerrain.terrainData.SetHeights (0, 0, arrHeights);
	}
	
	
	private void SteepnessErode ()
	{
		float[,] arrHeights = thisTerrain.terrainData.GetHeights (0, 0, xRes, zRes);   // WARNING: pullPatch has dimensions float[vCount, uCount] !
		for (int i = 0; i < xRes; i++) { //xRes
			for (int j = 0; j < zRes; j++) { //zRes
				
				float steep = thisTerrain.terrainData.GetSteepness(((float)(i)/xRes),((float)(j)/zRes));
				
				arrHeights[j, i] -= steep/50000;//(nv.y/100);   
			}
		}
		heights = arrHeights;
		thisTerrain.terrainData.SetHeights (0, 0, arrHeights);
		
	}
	
	private void WindErode (){
		float[,] arrHeights = thisTerrain.terrainData.GetHeights (0, 0, xRes, zRes);   // WARNING: pullPatch has dimensions float[vCount, uCount] !
		for (int i = 0; i < xRes; i++) { //xRes
			for (int j = 0; j < zRes; j++) { //zRes
				float steep = thisTerrain.terrainData.GetSteepness(((float)(i)/xRes),((float)(j)/zRes));
				arrHeights[j, i] -= steep/50000;//(nv.y/100);		
			}
		}
		heights = arrHeights;
		thisTerrain.terrainData.SetHeights (0, 0, arrHeights);
	}

 public void applyTexture()
	{
		splatmapData = thisTerrain.terrainData.GetAlphamaps(0, 0, thisTerrain.terrainData.alphamapWidth, thisTerrain.terrainData.alphamapHeight);

		for (int x = 0; x < thisTerrain.terrainData.alphamapWidth; x++) {
			for (int z = 0; z < thisTerrain.terrainData.alphamapHeight; z++) {
				// Normalise x/y coordinates to range 0-1 
				float z_01 = (float)z / (float)thisTerrain.terrainData.alphamapHeight;
				float x_01 = (float)x / (float)thisTerrain.terrainData.alphamapWidth;
				
				// Calculate the normal 
				Vector3 normal = thisTerrain.terrainData.GetInterpolatedNormal (z_01, x_01);
				float steepness = thisTerrain.terrainData.GetSteepness (z_01, x_01);

				// Declare a list of floats to hold the array of alpha values
				float[] splatVals = new float[thisTerrain.terrainData.alphamapLayers];

				// splatVals[0] (Soil) represents the base texture
				splatVals [0] = 0.5f;

				splatVals [2] = 1f;
				//if (steepness > rockSlope){
					splatVals [2] = 1.0f - Mathf.Clamp01 (steepness * steepness / (thisTerrain.terrainData.heightmapHeight / 5.0f));
				//}
				
	
				splatVals [1] = Mathf.Clamp01 (steepness * steepness / (thisTerrain.terrainData.heightmapHeight / 0.05f));
					
				// Sum of all splat date must = 1
				float n = splatVals.Sum ();
				
				// Each layer gets normalized so the sum = 1
				for (int i = 0; i<thisTerrain.terrainData.alphamapLayers; i++) {
					
					// Normalize so that sum of all texture weights = 1
					splatVals [i] /= n;
					
					// Assign this point to the splatmap array
					splatmapData [x, z, i] = splatVals [i];
					}
			}
		}
		// and finally assign the new splatmap to the terrainData:
		thisTerrain.terrainData.SetAlphamaps(0, 0, splatmapData);
		
	}

	public void clearAlphaMaps()
	{
		
		for (int x = 0; x < thisTerrain.terrainData.alphamapWidth; x++) {
			for (int z = 0; z < thisTerrain.terrainData.alphamapHeight; z++) {
				for (int k = 0; k < thisTerrain.terrainData.alphamapLayers; k++) {
					splatmapData[x,z,k] = 0.0f;
				}
			}
		}
		
		thisTerrain.terrainData.SetAlphamaps(0, 0, splatmapData);
		//Debug.Log("cleared");
		
	}


	public Vector2 worldToTerrainUV(Vector3 point) {
		return new Vector2 (point.x / size.x, point.z / size.z);
	}

	public Vector3 terrainUVToWorld(Vector2 uv) {
		float h = thisTerrain.terrainData.GetInterpolatedHeight (uv.x, uv.y);
		return new Vector3 (uv.x * size.x, h, uv.y * size.z);
	}

	public void terrainUVToPixel(Vector2 uv, ref int iVal, ref int jVal) {
		iVal = Mathf.RoundToInt(uv.x * xRes);
		jVal = Mathf.RoundToInt(uv.y * zRes);
	}

	public void RiverErode() {
		int fn = 10; 				// furthest neighbor count (how much this affects other pixels)
		float scale = 0.001f;   	// global river erosion factor (heuristic)
		bool[,] hit = new bool[xRes, zRes];
		float[,] prevH = thisTerrain.terrainData.GetHeights (0, 0, xRes, zRes);

		for (int l = waterSpots.Count, i = 0; i < l; i++) {
			WaterSeed ws = waterSpots[i];
			if (ws.erosionStrength <= 0) continue;  // 
			int basinHitCount = 0;
			for (int ll = ws.basinVertices.Count, j = 0; j < ll; j++) {
				int I = 0, J = 0;
				terrainUVToPixel(ws.basinVertices[j].uv, ref I, ref J);

				if (hit[I, J]) continue;

				hit[I, J] = true;
				basinHitCount++;

				// compute neighbour# and strength based on steepness and relative length run
				int N = (int) (ws.basinVertices[j].normal.y * fn * (basinHitCount / 10)) + 1;  // affect more neighbors if flatter
				float strength = ws.erosionStrength * (1 - ws.basinVertices[j].normal.y);     // erode less if flatter

				// center
				prevH[J, I] -= scale * strength;	

				for (int u = -N; u <= N; u++) {
					if (I + u < 0 || I + u > xRes) continue;  // if outside terrain heightmap pixels
					for (int v = -N; v <= N; v++) {
						if (J + v < 0 || J + v > zRes) continue;  // if outside terrain heightmap pixels

						prevH[J + v, I + u] -= scale * strength 
							- scale * strength * Mathf.Clamp01( (u*u + v*v) / (N*N) );  // note flipped i,j here

					}
				}

			}
			ws.erosionStrength -= 0.1f;
		}

		thisTerrain.terrainData.SetHeights (0, 0, prevH);
//		Debug.Log ("hit " + hitCount.ToString ());
		
	}

	public void storeCurrentPosition() {
		prevPosition = FPC.transform.position;
	}

	public void resetPrevPosition() {
		float targetH = thisTerrain.terrainData.GetInterpolatedHeight (prevPosition.x / size.x, prevPosition.z / size.z);
		Vector3 targetPos = new Vector3 (prevPosition.x, targetH + FPCCont.height / 2 + 1, prevPosition.z);
		FPC.transform.position = targetPos;
	}


	public void toggleMainCamera() {
		FPCCamera.enabled = !FPCCamera.enabled;
//		FPCCameraScript.enabled = !FPCCameraScript.enabled;
		orbitCamera.enabled = !orbitCamera.enabled;
		orbitCameraScript.enabled = !orbitCameraScript.enabled;
	}

	public void enableCursor(bool flag) {
		cursorProjector.enabled = flag;
	}

	public void resetTerrain() {
		mountainSpots = new List<MountainSeed>();
		waterSpots = new List<WaterSeed>();
		year = 0;
		Start ();
	}



	
	
//	███╗   ███╗ ██████╗ ██╗   ██╗███╗   ██╗████████╗ █████╗ ██╗███╗   ██╗
//	████╗ ████║██╔═══██╗██║   ██║████╗  ██║╚══██╔══╝██╔══██╗██║████╗  ██║
//	██╔████╔██║██║   ██║██║   ██║██╔██╗ ██║   ██║   ███████║██║██╔██╗ ██║
//	██║╚██╔╝██║██║   ██║██║   ██║██║╚██╗██║   ██║   ██╔══██║██║██║╚██╗██║
//	██║ ╚═╝ ██║╚██████╔╝╚██████╔╝██║ ╚████║   ██║   ██║  ██║██║██║ ╚████║
//	╚═╝     ╚═╝ ╚═════╝  ╚═════╝ ╚═╝  ╚═══╝   ╚═╝   ╚═╝  ╚═╝╚═╝╚═╝  ╚═══╝

	public class MountainSeed {
		public Terrain_holistic T;
		public Vector3 position;
		public float radius;
		public float currentHeight, targetHeight, normHeight;
		public int randomSeed;
		public int perlinU, perlinV;
		public int u0, v0;
		public float[,] perlinMap;
		public float scale = 0.02f;
		public bool active = false;

		public MountainSeed(Terrain_holistic _T, Vector3 _position, float _radius) {
			T = _T;
			position = _position;
			radius = 2 * _radius;
			active = true;
			currentHeight = 0;
			targetHeight = 100000 * radius;
			newPerlin();
			updateNormHeight();
		}

		public void newPerlin() {
			Random.seed = System.DateTime.Now.Millisecond;
			float off = 1000 * Random.value;
			jitter ();
			perlinMap = new float[perlinU, perlinV];
			float R = (float) (perlinU) / 2;
			float TAU_Q = Mathf.PI / 2;
			for (int i = 0; i < perlinU; i++) {
				for (int j = 0; j < perlinV; j++) {
					float dx = i - R;
					float dz = j - R;
					float r = Mathf.Sqrt(dx * dx + dz * dz);
					float n = Mathf.Cos(TAU_Q * Mathf.Clamp(r / R, 0.0f, 1.0f));
					perlinMap[i, j] = n * Mathf.PerlinNoise(scale * i * T.xPixSize + off, scale * j * T.zPixSize + off);
				}
			}
		}

		public void applyToTerrain() {
			if (active) {
				float[,] pullPatch = T.thisTerrain.terrainData.GetHeights (u0, v0, perlinU, perlinV);   // WARNING: pullPatch has dimensions float[perlinV, perlinU] !

				for (int i = 0; i < perlinU; i++) {
					for (int j = 0; j < perlinV; j++) {
						pullPatch [j, i] += normHeight * T.pullStrength * perlinMap [i, j];  // note inverted order of j,i
					}
				}

				T.thisTerrain.terrainData.SetHeights (u0, v0, pullPatch);

				currentHeight += 0.001f * (targetHeight - currentHeight) / 2;
				updateNormHeight();

				jitter();

			} else {
//				Debug.Log ("this mspot is exhausted");
			}
		}

		/**
		 * Jitters current center a little and updates several dependant properties
		 */
		public void jitter() {
			int seed = System.DateTime.Now.Millisecond;
			Random.seed = seed;
			float dx = Random.Range (-1.0f, 1.0f) * radius * 0.1f;
			Random.seed = seed + 1;
			float dz = Random.Range (-1.0f, 1.0f) * radius * 0.1f;
			float newX = Mathf.Clamp (position.x + dx, 0, T.size.x);
			float newZ = Mathf.Clamp (position.z + dz, 0, T.size.z);

			position = new Vector3 (newX, position.y, newZ);
			perlinU = (int) (2 * radius / T.xPixSize);
			perlinV = (int) (2 * radius / T.zPixSize);
			u0 = Mathf.RoundToInt (position.x / T.size.x * T.xRes - perlinU / 2);
			v0 = Mathf.RoundToInt (position.z / T.size.z * T.zRes - perlinV / 2);

			// some sanity
			if (u0 + perlinU > T.xRes) {
				perlinU = T.xRes - u0 - 1; 
			}
			if (v0 + perlinV > T.zRes) {
				perlinV = T.zRes - v0 - 1; 
			}
			if (u0 < 0) {
				u0 = 0;
			}
			if (v0 < 0) {
				v0 = 0;
			}

		}

		private void updateNormHeight() {
			normHeight = (targetHeight - currentHeight) / targetHeight;
			if (normHeight < 0.02)
				active = false;
		}




		public void logPerlin() {
			string l = "";
			for (int i = 0; i < perlinU; i++) {
				for (int j = 0; j < perlinV; j++) {
					l += perlinMap[i, j].ToString() + ",";
				}
				l += "|\n\r";
			}
			Debug.Log (l);
		}

		public override string ToString ()
		{
			return string.Format ("p = {0}, r = {1}", position.ToString(), radius.ToString());
		}

	}


	
	
//	██╗    ██╗ █████╗ ████████╗███████╗██████╗ 
//	██║    ██║██╔══██╗╚══██╔══╝██╔════╝██╔══██╗
//	██║ █╗ ██║███████║   ██║   █████╗  ██████╔╝
//	██║███╗██║██╔══██║   ██║   ██╔══╝  ██╔══██╗
//	╚███╔███╔╝██║  ██║   ██║   ███████╗██║  ██║
//	 ╚══╝╚══╝ ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝
	public class WaterSeed {
		private static float minimaLimit = 0.999f;

		public Terrain_holistic T;
		public BasinVertex source;					// position of water source in world coords
		public float prevHeight = 0.0f;
		public float updateThreshold = 2.0f;       // if heightDiffer < threshold, don't reset erosion strength

		public float flow;
		public int randomSeed;
		public bool active = false;

		public float erosionStrength = 1.0f;

		public List<BasinVertex> basinVertices;		// the basin points

		public WaterSeed(Terrain_holistic _T, Vector3 _position, float _flow) {
			T = _T;
			flow = _flow;

			Vector2 uv = T.worldToTerrainUV(_position);
			Vector3 n = T.thisTerrain.terrainData.GetInterpolatedNormal(uv.x, uv.y);
			source = new BasinVertex(_position, uv, n);

//			Debug.Log(_position.ToString());
//			Debug.Log (uv.ToString());
//			Debug.Log (n.ToString());

			updateBasin();
		}

		/**
		 * To be invoked any time terrain changes and this needs update...
		 */
		public void updateHeight() {
			float h = T.thisTerrain.terrainData.GetInterpolatedHeight (source.uv.x, source.uv.y);

			if (Mathf.Abs (h - prevHeight) > updateThreshold) {
				prevHeight = h;
				source.position = new Vector3 (source.position.x, h, source.position.z);
				source.normal = T.thisTerrain.terrainData.GetInterpolatedNormal(source.uv.x, source.uv.y);
				
				updateBasin();
			}
		}


		private void updateBasin() {
			// Reset collection
			basinVertices = new List<BasinVertex>();

			// Start eroding again
			erosionStrength = 1.0f;

			// Add source
			basinVertices.Add(source);

			int safe = 300;
			int step = 0;

			BasinVertex prev = source;

			// gradient descent (gross sort of...)
			while (step < safe) {
				Vector3 p = prev.position + 10 * prev.normal;
				Vector2 puv = T.worldToTerrainUV(p);
				Vector3 pn = T.thisTerrain.terrainData.GetInterpolatedNormal(puv.x, puv.y);
				Vector3 pp = T.terrainUVToWorld(puv);
				// reached a local minimum?
				if (pn.y > minimaLimit) {
					//Debug.Log("Local minimum at " + pp.ToString() + ", n.y: " + pn.y.ToString());
					break;  
				}
				

				BasinVertex v = new BasinVertex(pp, puv, pn);
				basinVertices.Add(v);

				prev = v;
				step++;
			}

			//Debug.Log ("computed " + basinVertices.Count.ToString() + " basin points");

		}

		public override string ToString ()
		{
			return string.Format ("p = {0}, n = {1}", source.position.ToString(), source.normal.ToString());
		}
	}

	public class BasinVertex {
		public Vector3 position;		// the basin points in world coordinates
		public Vector2 uv;				// basin points in normalized terrain coords
		public Vector2 px;				// i,j coords if the nearest heightmap pixel
		public Vector3 normal; 			// corresponding normals per point

		public BasinVertex(Vector3 _position, Vector2 _uv, Vector3 _normal) {
			position = _position;
			uv = _uv;
			normal = _normal;
		}
	}

}
