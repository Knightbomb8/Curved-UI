using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class BadVRCurvedUIEffect : BaseMeshEffect
{
    [Tooltip("Check to skip tesselation pass on this object. CurvedUI will not create additional vertices to make this object have a smoother curve. Checking this can solve some issues if you create your own procedural mesh for this object. Default false.")]
    public bool DoNotTesselate = false;
    
    //stored references
    Canvas myCanvas;
    BadVRCurvedUISettings mySettings;
    Graphic myGraphic;
    Text myText;
    
    //variables we operate on
    bool m_tesselationRequired = true;
    [SerializeField] [HideInInspector] Vector4 savedTextUV0;
    [SerializeField] [HideInInspector] Vector2 savedRectSize;
    
    Matrix4x4 CanvasToWorld;
    Matrix4x4 CanvasToLocal;
    Matrix4x4 MyToWorld;
    Matrix4x4 MyToLocal;
    List<UIVertex> m_curvedVerts = new List<UIVertex>();
    List<UIVertex> m_tesselatedVerts = new List<UIVertex>();
    UIVertex[] m_quad = new UIVertex[4];
    bool curvingRequired = true;

    bool tesselationRequired {
        get { return m_tesselationRequired; }
        set { m_tesselationRequired = value;
            //Debug.Log(this.gameObject.name + " settting tess to " + value, this.gameObject);
        }
    }

    protected override void Awake()
    {
        base.Awake();

        myGraphic = GetComponent<Graphic>();
        myText = GetComponent<Text>();
    }
    
    protected override void OnEnable()
    {
        //find the settings object and its canvas.
        FindParentSettings();

        //If there is an update to the graphic, we cant reuse old vertices, so new tesselation will be required       
        if (myGraphic)
        {
            myGraphic.RegisterDirtyMaterialCallback(TesselationRequiredCallback);
            myGraphic.SetVerticesDirty();
        }

        //add text events and callbacks
        if (myText)
        {
            myText.RegisterDirtyVerticesCallback(TesselationRequiredCallback);
            Font.textureRebuilt += FontTextureRebuiltCallback;
        }
    }
    
    protected override void OnDisable()
    {
        //If there is an update to the graphic, we cant reuse old vertices, so new tesselation will be required
        if (myGraphic)
            myGraphic.UnregisterDirtyMaterialCallback(TesselationRequiredCallback);

        if (myText)
        {
            myText.UnregisterDirtyVerticesCallback(TesselationRequiredCallback);
            Font.textureRebuilt -= FontTextureRebuiltCallback;
        }
    }
    
    /// <summary>
    /// Subscribed to graphic componenet to find out when vertex information changes and we need to create new geometry based on that.
    /// </summary>
    void TesselationRequiredCallback()
    {
        tesselationRequired = true;
    }

    /// <summary>
    /// Called by Font class to let us know font atlas has ben rebuilt and we need to update our vertices.
    /// </summary>
    void FontTextureRebuiltCallback(Font fontie)
    {
        if (myText.font == fontie)
            tesselationRequired = true;
    }
    
    /// <summary>
    /// gets the parent settings
    /// </summary>
    /// <param name="forceNew"></param>
    /// <returns></returns>
    public BadVRCurvedUISettings FindParentSettings(bool forceNew = false)
    {
        if (mySettings == null || forceNew)
        {
            mySettings = GetComponentInParent<BadVRCurvedUISettings>();

            if (mySettings == null) return null;
            else
            {
                myCanvas = mySettings.GetComponent<Canvas>();
            }
        }

        return mySettings;
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!ShouldModify()) return;
        
        //check for changes in text font material that would mean a retesselation in required to get fresh UV's
        CheckTextFontMaterial();
        
        //if we need to retesselate or the app hasnt started we need to reset tesselation points and 
        //say curving must happen
        if (tesselationRequired || !Application.isPlaying)
        {
            //Prepare a list and get vertices from the vertex stream. These come as triangles.
            if (m_tesselatedVerts == null)
                m_tesselatedVerts = new List<UIVertex>();
            else
                m_tesselatedVerts.Clear();

            vh.GetUIVertexStream(m_tesselatedVerts);

            //TODO come back to this
            //subdivide them
            //TesselateGeometry(m_tesselatedVerts);

            //save the transform properties we last tesselated for.
            savedRectSize = (transform as RectTransform).rect.size;

            //set flag
            tesselationRequired = false;
            curvingRequired = true;
        }
        
        //CURVING VERTICES ---------------------------------------------------------//
        if (curvingRequired)
        {
            //update transformation matrices we're going to use in curving the verts.
            CanvasToWorld = myCanvas.transform.localToWorldMatrix;
            CanvasToLocal = myCanvas.transform.worldToLocalMatrix;
            MyToWorld = transform.localToWorldMatrix;
            MyToLocal = transform.worldToLocalMatrix;

            //prepare list
            if (m_curvedVerts == null)
                m_curvedVerts = new List<UIVertex>();


            //check if the old list size is the same otherwise reset the list size and then curve
            if (m_curvedVerts.Count == m_tesselatedVerts.Count)
            {
                //Debug.Log("count equal");
                for (int i = 0; i < m_curvedVerts.Count; i++)
                    m_curvedVerts[i] = CurveVertex(m_tesselatedVerts[i], mySettings.Angle, mySettings.GetCyllinderRadiusInCanvasSpace(), (myCanvas.transform as RectTransform).rect.size);
            }
            else
            {
                m_curvedVerts.Clear();

                for (int i = 0; i < m_tesselatedVerts.Count; i++)
                    m_curvedVerts.Add(CurveVertex(m_tesselatedVerts[i], mySettings.Angle, mySettings.GetCyllinderRadiusInCanvasSpace(), (myCanvas.transform as RectTransform).rect.size));
            }

            //set flags
            curvingRequired = false;
        }
        
        //SAVE CURVED VERTICES TO THE VERTEX HELPER------------------------//
        //They can come as quads or as triangles.
        vh.Clear();
        if (m_curvedVerts.Count % 4 == 0)
        {
            for (int i = 0; i < m_curvedVerts.Count; i += 4)
            {
                for (int v = 0; v < 4; v++)//create a quad
                    m_quad[v] = m_curvedVerts[i + v];

                vh.AddUIVertexQuad(m_quad); // add it to the list
            }
        }
        else vh.AddUIVertexTriangleStream(m_curvedVerts);
    }
    
    /// <summary>
        /// Map position of a vertex to a section of a circle. calculated in canvas's local space
        /// </summary>
        UIVertex CurveVertex(UIVertex input, float cylinder_angle, float radius, Vector2 canvasSize)
        {

            Vector3 pos = input.position;

            //calculated in canvas local space version:
            pos = CanvasToLocal.MultiplyPoint3x4(MyToWorld.MultiplyPoint3x4(pos));
            // pos = mySettings.VertexPositionToCurvedCanvas(pos);

            if (mySettings.Angle != 0)
            {

                float theta = (pos.x / canvasSize.x) * cylinder_angle * Mathf.Deg2Rad;
                radius += pos.z; // change the radius depending on how far the element is moved in z direction from canvas plane
                pos.x = Mathf.Sin(theta) * radius;
                pos.z += Mathf.Cos(theta) * radius - radius;

            }
            //4. write output
            input.position = MyToLocal.MultiplyPoint3x4(CanvasToWorld.MultiplyPoint3x4(pos));

            return input;
        }
    
    void CheckTextFontMaterial()
    {
        //we check for a sudden change in text's fontMaterialTexture. This is a very hacky way, but the only one working reliably for now.
        if (myText)
        {
            if (myText.cachedTextGenerator.verts.Count > 0 && myText.cachedTextGenerator.verts[0].uv0 != savedTextUV0)
            {
                //Debug.Log("tess req - texture");
                savedTextUV0 = myText.cachedTextGenerator.verts[0].uv0;
                tesselationRequired = true;
            }
        }
    }
    
    bool ShouldModify()
    {
        if (!this.IsActive()) return false;

        if (mySettings == null) FindParentSettings();

        if (mySettings == null || !mySettings.enabled || mySettings.Angle == 1) return false;

        return true;
    }
    
    /*void TesselateGeometry(List<UIVertex> verts)
    {

        Vector2 tessellatedSize = mySettings.GetTesslationSize();

        //find if we are aligned with canvas main axis
        TransformMisaligned = !(savedUp.AlmostEqual(Vector3.up.normalized));

        // Convert the list from triangles to quads to be used by the tesselation
        TrisToQuads(verts);


        //do not tesselate text verts. Text usually is small and has plenty of verts already.
#if CURVEDUI_TMP || TMP_PRESENT
        if (myText == null && myTMP == null && !DoNotTesselate)
        {
#else
            if (myText == null && !DoNotTesselate)
            {
#endif
            // Tesselate quads and apply transformation
            int startingVertexCount = verts.Count;
            for (int i = 0; i < startingVertexCount; i += 4)
                ModifyQuad(verts, i, tessellatedSize);

            // Remove old quads
            verts.RemoveRange(0, startingVertexCount);
        }
    }*/
    
}
