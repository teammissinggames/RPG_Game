using System.Collections.Generic;
using UnityEngine;

public class Oddment
{
    public int Chance;
    public int Count = 1;
    public List<string> Items = new List<string>();
}

public class OddmentItem
{

}

public class OddmentItemDrop : OddmentItem
{
    public int id = -1;
}

public class OddmentEnemyEncounter : OddmentItem
{
    public string backgroundPath = string.Empty;
    public List<int> enemyIds = new List<int>();
}

public class OddmentTable
{
    private int oddmentTotal = 0;
    private Oddment empty = new Oddment();
    private List<Oddment> oddments = new List<Oddment>();

    public void SetOddments(List<Oddment> oddments)
    {
        this.oddments.Clear();
        if (oddments == null)
        {
            LogManager.LogError("Null oddments passed to SetOddments.");
            return;
        }
        this.oddments.AddRange(oddments);
        oddmentTotal = CalcOddment();
    }

    public int CalcOddment()
    {
        int total = 0;
        foreach (var oddment in oddments)
            total += oddment.Chance;
        return total;
    }

    public Oddment Pick()
    {
        int pick = Random.Range(0, oddmentTotal);
        int total = 0;
        foreach(var oddment in oddments)
        {
            total += oddment.Chance;
            if (total >= pick)
                return oddment;
        }
        var count = oddments.Count;
        return count == 0 ? null : oddments[count];
    }
}