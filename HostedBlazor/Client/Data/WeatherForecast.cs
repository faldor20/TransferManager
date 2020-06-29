 namespace blaz.Data

{
     using System;
     using SharedData;
  public class TransferData
    {
      public  float Percentage {get;set;}
      public float FileSize {get;set;}
      public float FileRemaining{get;set;}
      public float Speed{get;set;}
      public  string Destination{get;set;}
      public  string Source{get;set;}
      public TransferStatus Status{get;set;}
      public DateTime StartTime{get;set;}
      public DateTime EndTime {get;set;}
      public Guid id {get;set;}
    }
    public enum Status
	{
		Loading,
		NoConnection,
		DataError,
		Connected,
	}
}  