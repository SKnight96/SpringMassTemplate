using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using System.Linq;
using System.Text;
using System;

// Check this out we can require components be on a game object!
[RequireComponent(typeof(MeshFilter))]

public class BParticleSimMesh : MonoBehaviour
{
    public struct BSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring
        public int attachedParticle;            // index of the attached other particle (use me wisely to avoid doubling springs and sprign calculations)
    }

    public struct BContactSpring
    {
        public float kd;                        // damping coefficient
        public float ks;                        // spring coefficient
        public float restLength;                // rest length of this spring (think about this ... may not even be needed o_0
        public Vector3 attachPoint;             // the attached point on the contact surface
    }

    public struct BParticle
    {
        public Vector3 position;                // position information
        public Vector3 velocity;                // velocity information
        public float mass;                      // mass information
        public BContactSpring contactSpring;    // Special spring for contact forces
        public bool attachedToContact;          // is thi sparticle currently attached to a contact (ground plane contact)
        public List<BSpring> attachedSprings;   // all attached springs, as a list in case we want to modify later fast
        public Vector3 currentForces;           // accumulate forces here on each step        
    }

    public struct BPlane
    {
        public Vector3 position;                // plane position
        public Vector3 normal;                  // plane normal
    }

    public float contactSpringKS = 1000.0f;     // contact spring coefficient with default 1000
    public float contactSpringKD = 20.0f;       // contact spring daming coefficient with default 20

    public float defaultSpringKS = 100.0f;      // default spring coefficient with default 100
    public float defaultSpringKD = 1.0f;        // default spring daming coefficient with default 1

    public bool debugRender = false;            // To render or not to render


    /*** 
     * I've given you all of the above to get you started
     * Here you need to publicly provide the:
     * - the ground plane transform (Transform)
     * - handlePlaneCollisions flag (bool)
     * - particle mass (float)
     * - useGravity flag (bool)
     * - gravity value (Vector3)
     * Here you need to privately provide the:
     * - Mesh (Mesh)
     * - array of particles (BParticle[])
     * - the plane (BPlane)
     ***/
    public Transform planeTransform;
    public bool handlePlaneCollisions;
    public float particleMass;
    public bool useGravity;
    public Vector3 gravVal;
    private Mesh mesh;
    private BParticle[] particles;
    private BPlane plane;

    private int[] particleMeshMap;
    private Vector3[] newLocals;

    /// <summary>
    /// Init everything
    /// HINT: in particular you should probbaly handle the mesh, init all the particles, and the ground plane
    /// HINT 2: I'd for organization sake put the init particles and plane stuff in respective functions
    /// HINT 3: Note that mesh vertices when accessed from the mesh filter are in local coordinates.
    ///         This script will be on the object with the mesh filter, so you can use the functions
    ///         transform.TransformPoint and transform.InverseTransformPoint accordingly 
    ///         (you need to operate on world coordinates, and render in local)
    /// HINT 4: the idea here is to make a mathematical particle object for each vertex in the mesh, then connect
    ///         each particle to every other particle. Be careful not to double your springs! There is a simple
    ///         inner loop approach you can do such that you attached exactly one spring to each particle pair
    ///         on initialization. Then when updating you need to remember a particular trick about the spring forces
    ///         generated between particles. 
    /// </summary>
    void Start()
    {
        //int[] particleMeshMap = new int[1];

        mesh = GetComponent<MeshFilter>().mesh;
        newLocals = mesh.vertices;
        (particles, particleMeshMap) = InitParticles(particles, mesh, particleMeshMap);
        plane = InitPlane();

        Debug.Log(string.Join(", ", particles.Select(d => $"[position: {d.position}]")));
        Vector3[] worldPos = new Vector3[newLocals.Length];
        for (int i = 0; i < newLocals.Length; i++)
        {
            worldPos[i] = transform.TransformPoint(newLocals[i]);
        }
        Debug.Log(string.Join(", ", worldPos.Select(v => v.ToString())));
        Debug.Log(string.Join(", ", particleMeshMap));
    }



    /*** BIG HINT: My solution code has as least the following functions
     * InitParticles()
     * InitPlane()
     * UpdateMesh() (remember the hint above regarding global and local coords)
     * ResetParticleForces()
     * ...
     ***/



    /// <summary>
    /// Draw a frame with some helper debug render code
    /// </summary>
    public void Update()
    {
        // This will work if you have a correctly made particles array
        if (debugRender)
        {
            int particleCount = particles.Length;
            for (int i = 0; i < particleCount; i++)
            {
                Debug.DrawLine(particles[i].position, particles[i].position + particles[i].currentForces, Color.blue);

                int springCount = particles[i].attachedSprings.Count;
                for (int j = 0; j < springCount; j++)
                {
                    // if distance between two particles != restLength, draw violet instead
                    float springLength = Vector3.Distance(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position);
                    float naturalRestingLength = particles[i].attachedSprings[j].restLength;
                    bool withinThresh = Math.Abs(springLength - naturalRestingLength) < 0.07;
                    if (!withinThresh)
                    {
                        Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.magenta);
                    } else
                    {
                        Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.red);
                    }


                    //Debug.DrawLine(particles[i].position, particles[particles[i].attachedSprings[j].attachedParticle].position, Color.red);
                }
            }
        }
        //*/

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log(string.Join(", ", particles.Select(d => $"[position: {d.position}]")));
            Vector3[] worldPos = new Vector3[newLocals.Length];
            for (int i = 0; i < newLocals.Length; i++)
            {
                worldPos[i] = transform.TransformPoint(newLocals[i]);
            }
            Debug.Log(string.Join(", ", worldPos.Select(v => v.ToString())));
        }

    }

    public void FixedUpdate()
    {
        //start by resetting forces
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].currentForces = particles[i].mass * gravVal;
        }

        //spring forces
        for (int i = 0; i < particles.Length; i++)
        {
            for (int j = 0; j < particles[i].attachedSprings.Count; j++)
            {
                float springKS = particles[i].attachedSprings[j].ks;
                float springKD = particles[i].attachedSprings[j].kd;
                float springRestLength = particles[i].attachedSprings[j].restLength;
                int attachedParticleIndex = particles[i].attachedSprings[j].attachedParticle;

                if (i >= attachedParticleIndex)//already been here
                {
                    continue;
                }

                Vector3 deltaX = particles[i].position - particles[attachedParticleIndex].position;
                float magnitudeX = deltaX.magnitude;

                //consider magnitude = 0?
                //if (magnitudeX > 0.0001f)
                //{
                Vector3 dir = deltaX / magnitudeX;
                Vector3 springTerm = springKS * (springRestLength - magnitudeX) * dir;
                Vector3 deltaV = particles[i].velocity - particles[attachedParticleIndex].velocity;
                float dotNormal = Vector3.Dot(deltaV, dir);
                Vector3 dampingTerm = springKD * dotNormal * dir;
                Vector3 addedForce = springTerm - dampingTerm;

                particles[i].currentForces += addedForce;
                particles[attachedParticleIndex].currentForces -= addedForce;
                //}
            }
        }

        //checking for collision
        for (int i = 0; i < particles.Length; i++)
        {

            Vector3 planePos = plane.position;
            Vector3 planeNorm = plane.normal;
            Vector3 temp = particles[i].position - planePos;
            float particleDistance = Vector3.Dot(temp, planeNorm);
            if (particleDistance <= 0f)
            {
                if (particles[i].attachedToContact == false)
                {
                    particles[i].contactSpring = new BContactSpring//make new contact spring
                    {
                        kd = contactSpringKD,
                        ks = contactSpringKS,
                        restLength = 0f,//wont be using this anyway
                        attachPoint = particles[i].position - (planeNorm * particleDistance)
                    };
                    particles[i].attachedToContact = true;
                }
                //use contact spring
                Vector3 penVec = particles[i].position - particles[i].contactSpring.attachPoint;
                float penDepth = Vector3.Dot(penVec, planeNorm);
                Vector3 springTerm = -particles[i].contactSpring.ks * penDepth * planeNorm;

                Vector3 dampingTerm = particles[i].contactSpring.kd * particles[i].velocity;

                Vector3 penaltyForce = springTerm - dampingTerm;

                particles[i].currentForces += penaltyForce;

                ///sticky plane?
                /*float vNorm = Vector3.Dot(particles[i].velocity, planeNorm);
                Vector3 damping = particles[i].contactSpring.kd * vNorm * planeNorm;

                if (vNorm < 0f)
                {
                    Vector3 penaltySpringForce = spring - damping;
                    particles[i].currentForces += penaltySpringForce;
                } else
                {
                    particles[i].currentForces += spring;
                }*/


            } else
            {
                particles[i].attachedToContact = false;
            }
        }
        //final applications
        for (int i = 0; i < particles.Length; i++) {
            particles[i].velocity += (particles[i].currentForces / particles[i].mass) * Time.fixedDeltaTime;
            particles[i].position += particles[i].velocity * Time.fixedDeltaTime;
        }

        //localized particles to be used by mesh
        newLocals = UpdateMesh(mesh, particles, particleMeshMap);

        mesh.vertices = newLocals;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    (BParticle[] uniques, int[] map) InitParticles(BParticle[] particles, Mesh mesh, int[] particleMeshMap)
    {
        Vector3[] vertices = mesh.vertices;
        particleMeshMap = new int[vertices.Length];
        List<Vector3> uniqueVertices = new List<Vector3>();

        for (int i = 0; i < vertices.Length; i++)// far as I can tell the notion of a vertice map will pan out
        //the same each time. this whole process might be replaced by a single predefined array.
        {
            Vector3 worldPos = transform.TransformPoint(vertices[i]);
            int curIndex = -1;
            for (int j = 0; j < uniqueVertices.Count; j++)
            {
                if (Vector3.SqrMagnitude(uniqueVertices[j] - worldPos) < 0.000001f)
                {
                    curIndex = j;
                    break;
                }
            }
            if (curIndex == -1)
            {
                uniqueVertices.Add(worldPos);
                particleMeshMap[i] = uniqueVertices.Count - 1;
            }
            else
            {
                particleMeshMap[i] = curIndex;
            }
        }

        particles = new BParticle[uniqueVertices.Count];
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i] = new BParticle
            {
                position = uniqueVertices[i],
                velocity = Vector3.zero,
                mass = particleMass,
                /*contactSpring = new BContactSpring
                {
                    ks = 0f,
                    kd = 0f,
                    restLength = 0f,
                    attachPoint = Vector3.zero
                },*/
                attachedToContact = false,
                attachedSprings = new List<BSpring>(),//any way we can set this up here?
                currentForces = Vector3.zero//I think this makes sense?
            };
        }

        for (int i = 0; i < particles.Length; i++)
        {
            for (int j = i + 1; j < particles.Length; j++)
            {
                float dist = Vector3.Distance(particles[i].position, particles[j].position);
                BSpring spring1 = new BSpring
                {
                    kd = defaultSpringKD,
                    ks = defaultSpringKS,
                    restLength = dist,
                    attachedParticle = j
                };
                particles[i].attachedSprings.Add(spring1);
                BSpring spring2 = new BSpring
                {
                    kd = defaultSpringKD,
                    ks = defaultSpringKS,
                    restLength = dist,
                    attachedParticle = i
                };
                particles[j].attachedSprings.Add(spring2);
            }
        }

        int[] testmap = { 0, 1, 2, 3, 4, 5, 6, 7, 2, 3, 4, 5, 6, 0, 1, 7, 1, 3, 5, 7, 6, 4, 2, 0 };
        //return (particles, particleMeshMap);// need to return the mesh map too for later reassembly.
        return (particles, testmap); // coming back to this later. initial tests seem that dymatic calcs are totally unneeded
    }


    BPlane InitPlane()
    {
        BPlane plane = new BPlane()
        {
            position = planeTransform.position,
            normal = planeTransform.up
        };
        return plane;
    }

    Vector3[] UpdateMesh(Mesh mesh, BParticle[] particles, int[] particleMeshMap)
    {
        Vector3[] locals = mesh.vertices;
        for (int i = 0; i < locals.Length; i++)
        {
            Vector3 worldPos = particles[particleMeshMap[i]].position;
            locals[i] = transform.InverseTransformPoint(worldPos);
        }

        return locals;
    }
}
