using UnityEngine;
using System.Collections;

public class EVM : MonoBehaviour {

    //Assign variables
    bool showBS = false;

    WebCamTexture WCT;
    Texture2D snapshot, diffABS, diffThres, actual;

    Color[] pix, pix2, pixActual;

    int area, xPos, yPos, xPosOld, yPosOld, y4Head, xMin, xMax, yMin, yMax;
    public float threshold;
    public int aoiOffset, thresholdOffset, pulseOffset;

    void Start () {

        //Assign textures
        WCT = new WebCamTexture();
        GetComponent<Renderer>().material.mainTexture = WCT;
        WCT.Play();

        snapshot = new Texture2D(WCT.width, WCT.height);
        diffABS = new Texture2D(WCT.width, WCT.height);
        diffThres = new Texture2D(WCT.width, WCT.height);
        actual = new Texture2D(WCT.width, WCT.height);

        GameObject.Find("Snap").GetComponent<Renderer>().material.mainTexture = snapshot;
        GameObject.Find("DiffABS").GetComponent<Renderer>().material.mainTexture = diffABS;
        GameObject.Find("DiffThres").GetComponent<Renderer>().material.mainTexture = diffThres;
        GameObject.Find("CubeActual").GetComponent<Renderer>().material.mainTexture = actual;

    }

    void Update() {

        //Take snapshot
        if (Input.GetKeyDown(KeyCode.F1)) {
            pix = WCT.GetPixels();
            snapshot.SetPixels(pix);
            snapshot.Apply();
        }

        //Start calculations
        if (Input.GetKeyDown(KeyCode.F2)) {
            showBS = !showBS;
        }

        if (!showBS) {
            //For the CubeActual, when there are no calculation going on, just show
            //the webcam
            pixActual = WCT.GetPixels();
            actual.SetPixels(pixActual);
            actual.Apply();
        }

        if (showBS) {
            //Assign pixels to arrays
            pixActual = WCT.GetPixels();
            pix = WCT.GetPixels();
            pix2 = snapshot.GetPixels();

            // Absolute difference between snapshot and live video
            for (int i = 0; i < pix.Length; i++) {
                pix2[i].r = Mathf.Abs(pix[i].r - pix2[i].r);
                pix2[i].g = Mathf.Abs(pix[i].g - pix2[i].g);
                pix2[i].b = Mathf.Abs(pix[i].b - pix2[i].b);
            }

            diffABS.SetPixels(pix2);
            diffABS.Apply();
            // END ABSOLUTE DIFFERENCE --------------------------

            //

            // THRESHOLD ----------------------------------------
            for (int y = 0; y < WCT.height; y++) {
                for (int x = 0; x < WCT.width; x++) {
                    int i = y * WCT.width + x;
                    if (pix2[i].r + pix2[i].g + pix2[i].b > threshold && y > thresholdOffset) {
                        pix2[i] = Color.white;
                    }
                    else {
                        pix2[i] = Color.black;
                    }
                }
            }

            //diffThres.SetPixels(pix2);
            //diffThres.Apply();

            xPosOld = xPos;
            yPosOld = yPos;

            area = 0;
            xPos = 0;
            yPos = 0;

            for (int i = 0; i < pix.Length; i++) {
                if (pix2[i] == Color.white) {
                    area++;
                    xPos += i % WCT.width;
                    yPos += i / WCT.width;
                }
            }

            xPos /= area;
            yPos /= area;
        }
        // THRESHOLD END -------------------------------------------

        //

        // DRAWING -----------------------------------------------------
        y4Head = yPos + aoiOffset;
        xMin = xPos - 50;
        xMax = xPos + 50;
        yMin = y4Head - 25;
        yMax = y4Head + 25;

        //Draw the center of mass y-line
        for (int y = 0; y < WCT.height; y++) {
            pixActual[y * WCT.width + xPos] = Color.green;
            pix2[y * WCT.width + xPos] = Color.green;
        }
        //Draw the center of mass x-line
        for (int x = 0; x < WCT.width; x++) {
            pixActual[y4Head * WCT.width + x] = Color.green;
            pix2[y4Head * WCT.width + x] = Color.green;
        }

        //Draw AOI bounding box y-line
        for (int y = 1; y < WCT.height - 1; y++) {
            for (int x = 1; x < WCT.width - 1; x++) {
                if (x > xMin && x < xMax && y > yMin && y < yMax) {
                    pixActual[y * WCT.width + x] = Color.green;
                    pix2[y * WCT.width + x] = Color.green;
                }
            }
        }

        //Debug.Log("AOI - xMin: " + xMin + " xMax: " + xMax + " yMin: " + yMin + " yMax: " + yMax);

        diffThres.SetPixels(pix2);
        diffThres.Apply();

        actual.SetPixels(pixActual);
        actual.Apply();

        // DRAWING END -------------------------------------------------

        //

        //Normalize the green channel within the ROI
        for (int y = yMin; y < yMax; y++) {
            for (int x = xMin; x < xMax; x++) {
                int i = y * WCT.width + x;
                float sum = pix[i].r + pix[i].g + pix[i].b + 0.00001f;
                pix[i].g /= sum;
            }
        }

        // Calculate mean green value of the ROI -----------------------
        //For the pulse algorithm
        float green = 0;
        int totalPixels = 0;
        int pulse;

        for (int y = yMin; y < yMax; y++) {
            for (int x = xMin; x < xMax; x++) {
                green += pix[y * WCT.width + x].g;
                totalPixels++;
            }
        }

        green /= totalPixels;
        pulse = (int)(pulseOffset * green);
        Debug.Log("Total pixels: " + totalPixels + " | Average green: " + green + " | Pulse estimate: " + pulse);
        // Calculate END -----------------------------------------------

        if (Input.GetKeyDown(KeyCode.F5))
            threshold -= 0.1f;
        if (Input.GetKeyDown(KeyCode.F6))
            threshold += 0.1f;
        /*if (Input.GetKeyDown(KeyCode.F3))
            StartCoroutine("Algorithm");
        if (Input.GetKeyDown(KeyCode.F4))
            StopCoroutine("Algorithm");
    }

    IEnumerator Algorithm() {
        // Calculate mean green value of the AOI -----------------------
        while (true) {
            for (int y = yMin; y < yMax; y++) {
                for (int x = xMin; x < xMax; x++) {
                    green += pix[y * WCT.width + x].g;
                    totalPixels++;
                }
            }

            green /= totalPixels;
            pulse = (int)(pulseOffset * green);

            Debug.Log("Total pixels: " + totalPixels + " | Average green: " + green + " | Pulse estimate: " + pulse);
            yield return new WaitForSeconds(1f);
        }*/
    }
}
