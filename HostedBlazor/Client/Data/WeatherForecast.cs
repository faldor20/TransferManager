 namespace blaz.Data

{
     using System;
     using SharedFs;
 /*  public class TransferData
    {
      public  float Percentage {get;set;}
      public  float FileSize {get;set;}
      public  float FileRemaining{get;set;}
      public  float Speed{get;set;}
      public  string Destination{get;set;}
      public  string Source{get;set;}
      public TransferStatus Status{get;set;}
      public DateTime StartTime{get;set;}
      public DateTime EndTime {get;set;}
      public int id {get;set;}
    } */
    public enum Status
	{
		Loading,
		NoConnection,
		DataError,
		Connected,
	}
}  