using System;
using QuantConnect;
using QuantConnect.Data;

public class AAADirectDataProvider : BaseData
{
    public override SubscriptionDataSource GetSource(
        SubscriptionDataConfig config,
        DateTime date,
        bool isLiveMode)
    {
        if (isLiveMode)
        {
            return new SubscriptionDataSource("https://www.bitstamp.net/api/ticker/", SubscriptionTransportMedium.Rest);
        }

        var source = $"http://my-ftp-server.com/{config.Symbol.Value}/{date:yyyyMMdd}.csv";
        return new SubscriptionDataSource(source, SubscriptionTransportMedium.RemoteFile);

        /*
        // Example of loading from the Object Store:
        return new SubscriptionDataSource(Bitstamp.KEY, 
            SubscriptionTransportMedium.ObjectStore);

        // Example of loading a remote zip file:
        return new SubscriptionDataSource(
            "https://cdn.quantconnect.com/uploads/multi_csv_zipped_file.zip",
            SubscriptionTransportMedium.RemoteFile,
            FileFormat.ZipEntryName);

        // Example of loading a remote zip file and accessing a CSV file inside it:
        return new SubscriptionDataSource(
            "https://cdn.quantconnect.com/uploads/multi_csv_zipped_file.zip#csv_file_10.csv",
            SubscriptionTransportMedium.RemoteFile,
            FileFormat.ZipEntryName);
        */
    }
}
