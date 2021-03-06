using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FDDC;
using static BussinessLogic;
using static HTMLEngine;

public class Contract
{
    public struct struContract
    {
        //公告id
        public string id;

        //甲方
        public string JiaFang;

        //乙方
        public string YiFang;

        //项目名称
        public string ProjectName;

        //合同名称
        public string ContractName;

        //合同金额上限
        public string ContractMoneyUpLimit;

        //合同金额下限
        public string ContractMoneyDownLimit;

        //联合体成员
        public string UnionMember;

        public string GetKey()
        {
            return id + ":" + JiaFang + ":" + YiFang;
        }
    }


    internal static struContract ConvertFromString(string str)
    {
        var Array = str.Split("\t");
        var c = new struContract();
        c.id = Array[0];
        c.JiaFang = Array[1];
        c.YiFang = Array[2];
        c.ProjectName = Array[3];
        if (Array.Length > 4)
        {
            c.ContractName = Array[4];
        }
        if (Array.Length > 6)
        {
            c.ContractMoneyUpLimit = Array[5];
            c.ContractMoneyDownLimit = Array[6];
        }
        if (Array.Length == 8)
        {
            c.UnionMember = Array[7];
        }
        return c;
    }

    internal static string ConvertToString(struContract contract)
    {
        var record = contract.id + "," +
                     contract.JiaFang + "," +
                     contract.YiFang + "," +
                     contract.ProjectName + "," +
                     contract.ContractName + ",";
        record += Normalizer.NormalizeNumberResult(contract.ContractMoneyUpLimit) + ",";
        record += Normalizer.NormalizeNumberResult(contract.ContractMoneyDownLimit) + ",";
        record += contract.UnionMember;
        return record;
    }


    public static List<struContract> Extract(string htmlFileName)
    {
        //模式1：只有一个主合同
        //模式2：只有多个子合同
        //模式3：有一个主合同以及多个子合同
        var ContractList = new List<struContract>();
        var fi = new System.IO.FileInfo(htmlFileName);
        Program.Logger.WriteLine("Start FileName:[" + fi.Name + "]");
        var node = HTMLEngine.Anlayze(htmlFileName);
        companynamelist = BussinessLogic.GetCompanyNameByCutWord(node);

        var Id = fi.Name.Replace(".html", "");
        Program.Logger.WriteLine("公告ID:" + Id);
        //主合同的抽取
        ContractList.Add(ExtractSingle(node, Id));
        return ContractList;
    }

    static List<struCompanyName> companynamelist;

    static struContract ExtractSingle(MyRootHtmlNode node, String Id)
    {
        var contract = new struContract();
        //公告ID
        contract.id = Id;
        //甲方
        contract.JiaFang = GetJiaFang(node);
        var trailingwords = new string[] { "（以下简称", "（下称", "（简称", "(以下简称", "(下称", "(简称" };
        //暂时不做括号的正规化
        foreach (var trailin in trailingwords)
        {
            if (contract.JiaFang.Contains(trailin))
            {
                contract.JiaFang = Utility.GetStringBefore(contract.JiaFang, trailin);
            }
        }
        contract.JiaFang = contract.JiaFang.Replace(" ", "");

        //乙方
        contract.YiFang = GetYiFang(node);
        //暂时不做括号的正规化
        foreach (var trailin in trailingwords)
        {
            if (contract.YiFang.Contains(trailin))
            {
                contract.YiFang = Utility.GetStringBefore(contract.YiFang, trailin);
            }
        }
        contract.YiFang = contract.YiFang.Replace(" ", "");

        //金额
        contract.ContractMoneyUpLimit = Normalizer.NormalizerMoney(GetMoney(node), "");

        contract.ContractMoneyDownLimit = contract.ContractMoneyUpLimit;
        //合同
        contract.ContractName = GetContractName(node);
        contract.ContractName = contract.ContractName.Replace(" ", "").ToLower();
        //项目
        contract.ProjectName = GetProjectName(node);
        if (contract.ProjectName == "" && contract.ContractName.EndsWith("项目合同"))
        {
            contract.ProjectName = contract.ContractName.Substring(0, contract.ContractName.Length - 2);
        }
        contract.ProjectName = contract.ProjectName.Replace(" ", "").ToLower();

        return contract;
    }


    static string GetJiaFang(MyRootHtmlNode root)
    {
        var Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.LeadingWordList = new string[] { "发包人：", "招标人：", "业主方：", "业主：", "甲方：", "采购人：", "采购人名称：" };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("甲方候补词(关键字)：[" + item + "]");
            return item;
        }

        //招标
        Extractor = new ExtractProperty();
        var StartArray = new string[] { "业主", "收到", "接到" };
        var EndArray = new string[] { "发来", "发出", "的中标" };
        Extractor.StartEndFeature = Utility.GetStartEndStringArray(StartArray, EndArray);
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            var JiaFang = item;
            JiaFang = JiaFang.Replace("业主", "");
            Program.Logger.WriteLine("甲方候补词(招标)：[" + item + "]");
            return item;
        }

        //合同
        Extractor = new ExtractProperty();
        StartArray = new string[] { "与", "与业主" };
        EndArray = new string[] { "签署", "签订" };
        Extractor.StartEndFeature = Utility.GetStartEndStringArray(StartArray, EndArray);
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("甲方候补词(合同)：[" + item + "]");
            return item;
        }
        return "";
    }



    static string GetContractName(MyRootHtmlNode root)
    {
        var Extractor = new ExtractProperty();
        var MarkFeature = new ExtractProperty.struMarkFeature();
        MarkFeature.MarkStartWith = "《";
        MarkFeature.MarkEndWith = "》";
        MarkFeature.InnerEndWith = "合同";

        var MarkFeatureConfirm = new ExtractProperty.struMarkFeature();
        MarkFeatureConfirm.MarkStartWith = "《";
        MarkFeatureConfirm.MarkEndWith = "》";
        MarkFeatureConfirm.InnerEndWith = "确认书";


        Extractor.MarkFeature = new ExtractProperty.struMarkFeature[] { MarkFeature, MarkFeatureConfirm };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("合同名称候补词（《XXX》）：[" + item + "]");
            return item;
        }

        Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.LeadingWordList = new string[] { "合同名称：" };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("合同名称候补词(关键字)：[" + item + "]");
            return item;
        }

        //合同
        Extractor = new ExtractProperty();
        var StartArray = new string[] { "签署了" };
        var EndArray = new string[] { "合同" };
        Extractor.StartEndFeature = Utility.GetStartEndStringArray(StartArray, EndArray);
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("合同候补词(合同)：[" + item + "]");
            return item.Replace(" ", "");
        }
        return "";
    }

    static string GetProjectName(MyRootHtmlNode root)
    {
        var Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.LeadingWordList = new string[] { "项目名称：" };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("项目名称候补词(关键字)：[" + item + "]");
            return item.Replace(" ", "");
        }

        var MarkFeature = new ExtractProperty.struMarkFeature();
        MarkFeature.MarkStartWith = "“";
        MarkFeature.MarkEndWith = "”";
        MarkFeature.InnerEndWith = "标段";

        var MarkFeatureConfirm = new ExtractProperty.struMarkFeature();
        MarkFeatureConfirm.MarkStartWith = "“";
        MarkFeatureConfirm.MarkEndWith = "”";
        MarkFeatureConfirm.InnerEndWith = "标";

        Extractor.MarkFeature = new ExtractProperty.struMarkFeature[] { MarkFeature, MarkFeatureConfirm };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("工程名称候补词（《XXX》）：[" + item + "]");
            return item;
        }

        var list = BussinessLogic.GetProjectName(root);
        if (list.Count > 0)
        {
            return list[0];
        }
        return "";
    }


    static string GetYiFang(HTMLEngine.MyRootHtmlNode root)
    {
        var Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.LeadingWordList = new string[] { "供应商名称：" };
        Extractor.Extract(root);
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("甲方候补词(关键字)：[" + item + "]");
            return item;
        }
        //乙方:"有限公司"

        Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.TrailingWordList = new string[] { "有限公司董事会" };
        Extractor.Extract(root);
        Extractor.CandidateWord.Reverse();
        foreach (var item in Extractor.CandidateWord)
        {
            Program.Logger.WriteLine("乙方候补词(关键字)：[" + item + "有限公司]");
            return item + "有限公司";
        }

        if (companynamelist.Count > 0)
        {
            return companynamelist[companynamelist.Count - 1].secFullName;
        }

        return "";
    }



    #region  Money
    static string GetMoney(HTMLEngine.MyRootHtmlNode node)
    {
        var Money = "";
        var Extractor = new ExtractProperty();
        //这些关键字后面
        Extractor.LeadingWordList = new string[] { "中标金额", "中标价", "合同金额", "合同总价", "订单总金额" };
        Extractor.Extract(node);
        foreach (var item in Extractor.CandidateWord)
        {
            Money = Utility.SeekMoney(item, "");
            Program.Logger.WriteLine("金额候补词：[" + Money + "]");
        }
        return Money;
    }
    #endregion
}