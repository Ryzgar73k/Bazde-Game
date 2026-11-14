using UnityEngine;

/// <summary>
/// Animator ile aynı GameObject'te durur. Animasyonun root motion hareketini (ileri sıçrama)
/// üretir ancak modeli kendisi hareket ettirmez; PlayerController bu değeri kapsüle uygular.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionRelay : MonoBehaviour
{
    Animator anim;
    public Vector3 AccumulatedDelta { get; private set; }

    void Awake() { anim = GetComponent<Animator>(); }

    void OnAnimatorMove()
    {
        AccumulatedDelta += anim.deltaPosition;
    }

    public Vector3 Consume()
    {
        var d = AccumulatedDelta;
        AccumulatedDelta = Vector3.zero;
        return d;
    }
}
