using UnityEngine;

public class ExampleRouletteDriver : MonoBehaviour
{
    public HorizontalTextRoulette roulette;

    void Start()
    {
        roulette.SetEntries(new System.Collections.Generic.List<string> {
            "Alice","Brandon","Casey","Drew","Elliot","Fiona","Gabe","Hana"
        });

        // Fast initial burst? Start at 3000 px/s, ramp to 4000 px/s with strong accel:
        // Spins now and auto-stops on index 3, using 60% of total time for decel:
        roulette.StartSpinAndAutoStop(index: 3, extraPasses: 1, decelPortion: 0.6f, initialSpeed: 3000f);


        // After a bit, ask it to stop at “Hana” after 2 more passes, decelerate over 1.6s:
        Invoke(nameof(StopNow), 2.0f);
    }

    void StopNow()
    {
        //roulette.StopAt("Hana", extraPasses: 1);
        roulette.OnStopped += (i, s) => Debug.Log($"Stopped on {i}:{s}");
    }
}
