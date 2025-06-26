namespace ConsoleApp2;

public class Program
{


    //balance
    private static decimal balance = 1000;
    private static decimal compundBalance = 0;
    private static decimal compundBetBalance = 0;
    private static decimal newsBalance = 0;


    //percent
    private static decimal s1minNetProfit = 0m;
    private static decimal s1maxNetProfit = 3m;

    private static decimal s2minNetProfit = 10;
    private static decimal s2maxNetProfit = 15;

    private static decimal compundPercent = 1.5m;
    private static decimal highRiskWithPercent = 25;
    private static decimal newsRiskWithPercent = 80;

    //settings
    private static int years = 1;
    private static int daysInYear = 222;
    
    private static bool isMonthlyWithdrawInPercent = true;
    private static bool hasMonthlyWithdraw = true;
    private static decimal mounthlyWithdraw = 5;

    private static bool hasMonthlyAddBalance = false;
    private static decimal monthlyAddBalanceCash = 100;

    private static decimal highRiskAtCash = 100;
    private static decimal highRiskScore = 0;
    private static decimal highRiskProfitFactor = 2;
    private static decimal highRiskTriggers = 5;
    private static List<int> highRiskWinRate = [1, 0, 0, 1, 0, 1, 0, 0, 1, 1];

    
    private static List<int> newsWinRate = [1, 1, 0, 1, 0, 1, 1, 0, 1, 1];
    private static decimal newsMinProfitFactor = 1.5m;
    private static decimal newsMaxProfitFactor = 2.5m;
    private static decimal newsLossFactor = 1;
    private static decimal newsTriggers = 1;
    private static bool newsHedging = true;

    //temp
    private static int currentMonth = 1;
    private static int currentWeek = 1;
    private static int currentDay = 1;
    private static decimal allWithdraw = 0;
    private static bool betDone = false;
    private static bool newsDone = false;


    public static void Main2(string[] args) 
    {
        for (int i = 1; i < (years * daysInYear); i++)
        {
            Console.WriteLine("\n-----------------------Day(" + i.ToString()+")-----------------------");

            currentDay = i;
            
            if (hasMonthlyAddBalance)
            {
                balance += monthlyAddBalanceCash;
            }
            
            SimulatehighRisk();
            SimulateS1();

            if (i % Math.Round((decimal)(daysInYear / 12.0 / 4.0))  == 0)
            {
                currentWeek = daysInYear / 12 / 4 / i;
                ResetWeeklyThings();
            }

            if (i % Math.Round(daysInYear / 12.0m) == 0)
            {
                currentMonth = daysInYear / 12 / i;

                SimulateNewsStrategy();

                if (hasMonthlyWithdraw)
                {
                    if (isMonthlyWithdrawInPercent)
                    {
                        decimal withdrawBalance = GetFullBalance() - allWithdraw;
                        allWithdraw += Utils.RandomDecimal(Math.Round(mounthlyWithdraw / 2) , mounthlyWithdraw) / 100 * withdrawBalance;
                    }
                    balance -= mounthlyWithdraw;
                    allWithdraw += mounthlyWithdraw;
                }
                ResetMonthlyThings();
            }
            ResetDailyThings();
            
            Console.WriteLine("Widraw: " + Math.Round(allWithdraw, 2));
            Console.WriteLine("GetFullBalance: $" + Math.Round(GetFullBalance()-allWithdraw, 2));
            Console.WriteLine("Compunt Balance: $" + Math.Round(compundBalance, 2));
            Console.WriteLine("Compunt Bet Balance: $" + Math.Round(compundBetBalance, 2));
            Console.WriteLine("News Balance: $" + Math.Round(newsBalance, 2));
            Console.WriteLine("Grow percent: %" + Math.Round(GetFullBalance() / balance * 100, 2));

            Console.WriteLine("-----------------------Day(" + i.ToString() + ")-----------------------\n");
        }
    }

    private static decimal SimulateS1() 
    {
        decimal randomProfitPercent = Utils.RandomDecimal(s1minNetProfit, s1maxNetProfit);
        
        if (randomProfitPercent > 0)
        {
            if (randomProfitPercent > compundPercent && compundBetBalance < highRiskAtCash)
            {
                compundBetBalance += (randomProfitPercent - compundPercent) / 100 * GetCompoundedBalance();
                compundBalance += (decimal)(compundPercent / 100) * GetCompoundedBalance();
            }
            else {
                compundBalance += randomProfitPercent / 100 * GetCompoundedBalance();
            }
        }
        else 
        {
            if (compundBalance > 0)
            {
                compundBalance += (randomProfitPercent - compundPercent) / 100 * GetCompoundedBalance();
            }
            else
            {
                balance += (randomProfitPercent - compundPercent) / 100 * GetCompoundedBalance();
            }

        }
        Console.WriteLine("Profit: $"+Math.Round(randomProfitPercent / 100 * GetCompoundedBalance(), 2)+" %"+randomProfitPercent);
        return 0;

    }

    private static decimal SimulateNewsStrategy()
    {
        if (newsHedging)
        {
            newsBalance += (newsRiskWithPercent / 100 * newsBalance) * Utils.RandomDecimal(newsMinProfitFactor, newsMaxProfitFactor);
            newsBalance += (newsRiskWithPercent / 100 * newsBalance) * newsLossFactor;
            return 0;
        }
        else
        {
            if (newsWinRate[new Random().Next(0, newsWinRate.Count - 1)] == 1)
            {
                newsBalance += (newsRiskWithPercent / 100 * newsBalance) * Utils.RandomDecimal(newsMinProfitFactor, newsMaxProfitFactor);
            }
            else
            {
                newsBalance += (newsRiskWithPercent / 100 * newsBalance) * newsLossFactor;
            }

            return 0;
        }
    }

    private static void SimulatehighRisk()
    {
        if (betDone) return;
        if (compundBetBalance >= highRiskAtCash) {
            for (int i = 0; i < highRiskTriggers; i++)
            {
                if (highRiskWinRate[new Random().Next(0, highRiskWinRate.Count - 1)] == 1)
                {
                    compundBetBalance += (highRiskWithPercent / 100 * compundBetBalance) * highRiskProfitFactor/2;
                    newsBalance += (highRiskWithPercent / 100 * compundBetBalance) * highRiskProfitFactor/2;
                }
                else {
                    compundBetBalance -= (highRiskWithPercent / 100 * compundBetBalance);
                }
            }
            betDone = true;
        }
    }

    private  static decimal GetFullBalance() => balance + compundBalance + compundBetBalance + newsBalance;
    private static decimal GetCompoundedBalance() => balance + compundBalance;
    private static decimal GetCompoundedBetBalance() => balance + compundBetBalance;

    private static void ResetMonthlyThings() {
        betDone = false;
    }
    private static void ResetWeeklyThings() {
    }

    private static void ResetDailyThings() { 
        
    }

}
