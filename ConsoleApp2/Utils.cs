namespace ConsoleApp2;

public class Utils
{
    
    public static decimal RandomDecimal(decimal min, decimal max, int calFloat = 0)
    {
        bool hasNoFloating = false;
        if (!min.ToString().Contains("."))
        {
            min = decimal.Parse(min.ToString()+"."+0);
        }
        if (!max.ToString().Contains("."))
        {
            max = decimal.Parse(max.ToString()+"."+0);
        }

        string minNumber = min.ToString().Split('.')[0];
        string maxNumber = max.ToString().Split('.')[0];
        string randNumber = (new Random().Next(int.Parse(minNumber), int.Parse(maxNumber))).ToString();

        string minFloting = min.ToString().Split(".")[1];


        string maxFloting = max.ToString().Split(".")[1];

        int maxFloatingmax = 0;
        if (calFloat > 0)
        {
            maxFloatingmax = calFloat;
        }
        else {
            if (minFloting.Length > maxFloting.Length)
            {
                string con = "";
                for (int i = 0; i < minFloting.Length; i++)
                {
                    con += "9";
                }
                maxFloatingmax = int.Parse(con);
            }
            else
            {
                string con = "";
                for (int i = 0; i < maxFloting.Length; i++)
                {
                    con += "9";
                }
                maxFloatingmax = int.Parse(con);
            }
        }

        string randFloatingNumber = hasNoFloating ? "0" : (new Random().Next(0, maxFloatingmax)).ToString();

        return decimal.Parse(randNumber + "." + randFloatingNumber);


    }


}