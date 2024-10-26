using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public class AnimationPreviewWindow : MonoBehaviour, IAnimationWindowPreview
{
    public Vector3 offset = Vector3.zero;

    private AnimationScriptPlayable m_Playable;
    private AnimationJob m_Job;
    private Vector3 m_CurrentOffset;

    struct AnimationJob : IAnimationJob
    {
        public TransformStreamHandle transform;
        public Vector3 offset;

        public void ProcessRootMotion(AnimationStream stream)
        {
            Vector3 position = transform.GetLocalPosition(stream);
            position += offset;

            transform.SetLocalPosition(stream, position);
        }

        public void ProcessAnimation(AnimationStream stream)
        {
        }
    }

    public void StartPreview()
    {
        m_CurrentOffset = offset;
    }

    public void StopPreview()
    {
    }

    public void UpdatePreviewGraph(PlayableGraph graph)
    {
        if (m_CurrentOffset != offset)
        {
            m_Job.offset = offset;
            m_Playable.SetJobData(m_Job);

            m_CurrentOffset = offset;
        }
    }

    public Playable BuildPreviewGraph(PlayableGraph graph, Playable input)
    {
        Animator animator = GetComponent<Animator>();

        m_Job = new AnimationJob();
        m_Job.transform = animator.BindStreamTransform(transform);
        m_Job.offset = offset;

        m_Playable = AnimationScriptPlayable.Create(graph, m_Job, 1);

        graph.Connect(input, 0, m_Playable, 0);

        return m_Playable;
    }

}
