namespace Agent_QC.Models;

public class AjaxResult
{
    public int Code { get; set; }
    public string Msg { get; set; } = "success";
    public object? Data { get; set; }

    public static AjaxResult Success(object? data = null, string msg = "success")
        => new() { Code = 200, Msg = msg, Data = data };

    public static AjaxResult Error(string msg)
        => new() { Code = 500, Msg = msg };

    public static AjaxResult Error(int code, string msg)
        => new() { Code = code, Msg = msg };
}
