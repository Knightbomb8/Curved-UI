using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BadVRCurvedUISettings : MonoBehaviour
{
    //TODO rework so it works with TMP as well
    [SerializeField]
    int angle = 90;
    [SerializeField]
    public bool preserveAspect = true;
    [SerializeField]
    float quality = 1f;

    [Tooltip("How many max segments the rng will conform too")]
    public int baseCircleSegments = 16;

    //stored variables
    Vector2 savedRectSize;
    float savedRadius;
    Canvas myCanvas;
    RectTransform m_rectTransform;

    /// <summary>
    /// changes layer to ui and saves the rect size
    /// </summary>
    void Awake()
    {
        // If this canvas is on Default layer, switch it to UI layer..
        // this is to make sure that when using raycasting to detect interactions, 
        // nothing will interfere with it.
        if (gameObject.layer == 0) this.gameObject.layer = 5;

        //save initial variables
        savedRectSize = RectTransform.rect.size;
    }
    
    void OnEnable()
    {
        //Redraw canvas object on enable.
        foreach (UnityEngine.UI.Graphic graph in (this).GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            graph.SetAllDirty();
        }
    }

    void OnDisable()
    {
        foreach (UnityEngine.UI.Graphic graph in (this).GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            graph.SetAllDirty();
        }
    }

    /// <summary>
    /// Adds the BadVRCurvedUIEffect component to every child gameobject that requires it. 
    /// BadVRCurvedUIEffect creates the curving effect.
    /// </summary>
    public void AddEffectToChildren()
    {
        foreach (UnityEngine.UI.Graphic graph in GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            if (graph.GetComponent<BadVRCurvedUIEffect>() == null)
            {
                graph.gameObject.AddComponent<BadVRCurvedUIEffect>();
                graph.SetAllDirty();
            }
        }
    }


    //getters and setters
    RectTransform RectTransform {
        get
        {
            if (m_rectTransform == null) m_rectTransform = transform as RectTransform;
            return m_rectTransform;
        }
    }
    
    /// <summary>
    /// The measure of the arc of the Canvas.
    /// </summary>
    public int Angle {
        get { return angle; }
    }
    
    /// <summary>
    /// Returns the radius of curved canvas cyllinder, expressed in Cavas's local space units.
    /// </summary>
    public float GetCyllinderRadiusInCanvasSpace()
    {
        float ret;
        if (preserveAspect)
        {
            ret = (RectTransform.rect.size.x / ((2 * Mathf.PI) * (angle / 360.0f)));
        }
        else
            ret = (RectTransform.rect.size.x * 0.5f) / Mathf.Sin(Mathf.Clamp(angle, -180.0f, 180.0f) * 0.5f * Mathf.Deg2Rad);

        return angle == 0 ? 0 : ret;
    }
    
    /// <summary>
    /// Tells you how big UI quads can get before they should be tesselate to look good on current canvas settings.
    /// Used by CurvedUIVertexEffect to determine how many quads need to be created for each graphic.
    /// </summary>
    public Vector2 GetTesslationSize(bool modifiedByQuality = true)
    {
        Vector2 ret = RectTransform.rect.size;
        if (Angle != 0 )
        {
            ret /= GetSegmentsByAngle(angle);
        }
        //Debug.Log(this.gameObject.name + " returning size " + ret + " which is " + ret * this.transform.localScale.x + " in world space.", this.gameObject);
        return ret / (modifiedByQuality ? Mathf.Clamp(quality, 0.01f, 10.0f) : 1);
    }
    
    float GetSegmentsByAngle(float angle)
    {
        if (Math.Abs(angle) <= 1)
            return 1;
        else if (Math.Abs(angle) < 90)//proportionaly twice as many segments for small angle canvases
            return baseCircleSegments * (Remap(Mathf.Abs(angle),0, 90, 0.01f, 0.5f));
        else
            return baseCircleSegments * (Remap(Mathf.Abs(angle),90, 360.0f, 0.5f, 1));

    }

    float Remap(float originalValue, float from1, float to1, float from2, float to2)
    {
        return 1;
    }
}
