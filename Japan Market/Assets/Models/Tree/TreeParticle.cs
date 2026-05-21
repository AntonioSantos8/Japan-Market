using UnityEngine;
using System.Collections.Generic;
public class TreeParticle : MonoBehaviour
{
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();


    private Dictionary<uint, Vector3> frozenRotations = new Dictionary<uint, Vector3>();

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];

        var collision = ps.collision;
        collision.enabled = true;
        collision.sendCollisionMessages = true;
    }

    void OnParticleCollision(GameObject other)
    {
        int numEvents = ps.GetCollisionEvents(other, collisionEvents);
        int count = ps.GetParticles(particles);
        for (int e = 0; e < numEvents; e++)
        {
            Vector3 hitPos = collisionEvents[e].intersection;

            for (int i = 0; i < count; i++)
            {
                uint id = particles[i].randomSeed;


                if (!frozenRotations.ContainsKey(id))
                {
                    if (Vector3.Distance(particles[i].position, hitPos) < 0.15f)
                    {
                        frozenRotations[id] = particles[i].rotation3D;
                    }
                }
            }
        }
    }

    void LateUpdate()
    {
        if (frozenRotations.Count == 0) return;

        int count = ps.GetParticles(particles);
        bool changed = false;

        for (int i = 0; i < count; i++)
        {
            uint id = particles[i].randomSeed;

            if (frozenRotations.TryGetValue(id, out Vector3 rot))
            {
                particles[i].velocity = Vector3.zero;
                particles[i].angularVelocity = 0;
                particles[i].angularVelocity3D = Vector3.zero;

            }
        }

        if (changed)
            ps.SetParticles(particles, count);
    }
}
