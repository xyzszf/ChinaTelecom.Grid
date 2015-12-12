﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.OleDb;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Authorization;
using Microsoft.Data.Entity;
using ChinaTelecom.Grid.Models;

namespace ChinaTelecom.Grid.Controllers
{
    [Authorize]
    public class RecordController : BaseController
    {
        public IActionResult Index(string ContractorName, ServiceStatus? Status, string Address, string Set, string Phone, string raw)
        {
            IEnumerable<Record> ret = DB.Records.AsNoTracking();
            if (!string.IsNullOrEmpty(ContractorName))
                ret = ret.Where(x => x.ContractorName == ContractorName);
            if (Status.HasValue)
                ret = ret.Where(x => x.Status == Status);
            if (!string.IsNullOrEmpty(Set))
                ret = ret.Where(x => x.Set == Set);
            if (!string.IsNullOrEmpty(Address))
                ret = ret.Where(x => x.ImplementAddress.Contains(Address) || x.StandardAddress.Contains(Address));
            if (!string.IsNullOrEmpty(Phone))
                ret = ret.Where(x => x.Phone.Contains(Phone));
            if (raw != "true")
            {
                ViewBag.Statuses = DB.Records.Select(x => x.Status.ToString()).Distinct().ToList();
                ViewBag.ContractorNames = DB.Records.Select(x => x.ContractorName).Distinct().ToList();
                ViewBag.Sets = DB.Records.Select(x => x.Set).Distinct().ToList();
                return PagedView(ret);
            }
            else
            {
                return XlsView(ret.ToList(), "chinatelecom.xls", "Xls");
            }
        }

        public IActionResult Import(IFormFile file)
        {
            var fname = Guid.NewGuid().ToString().Replace("-", "") + System.IO.Path.GetExtension(file.GetFileName());
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fname);
            file.SaveAs(path);
            string connStr;
            if (System.IO.Path.GetExtension(path) == ".xls")
                connStr = "Provider=Microsoft.Jet.OLEDB.4.0;" + "Data Source=" + path + ";" + ";Extended Properties=\"Excel 8.0;HDR=YES;IMEX=1\"";
            else
                connStr = "Provider=Microsoft.ACE.OLEDB.12.0;" + "Data Source=" + path + ";" + ";Extended Properties=\"Excel 12.0;HDR=YES;IMEX=1\"";
            using (var conn = new OleDbConnection(connStr))
            {
                conn.Open();
                var cmd = new OleDbCommand("select * from [Sheet1$]", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var record = new Record
                        {
                            Account = reader["接入号"].ToString(),
                            Status = (ServiceStatus)Enum.Parse(typeof(ServiceStatus), reader["用户状态"].ToString()),
                            CustomerName = reader["用户姓名"].ToString(),
                            ContractorName = reader["四级承包人名称"].ToString(),
                            ContractorStruct = reader["四级承包体名称"].ToString(),
                            CurrentMonthBill = Convert.ToDouble(reader["当月出帐"].ToString()),
                            MDS = reader["中投"].ToString(),
                            AgentFee = Convert.ToDouble(reader["代理费"].ToString()),
                            Commission = Convert.ToDouble(reader["一次佣金"].ToString()),
                            Arrearage = Convert.ToDouble(reader["欠费"].ToString()),
                            ImplementAddress = reader["装机地址"].ToString(),
                            StandardAddress = reader["标准地址"].ToString(),
                            Set = reader["套餐"].ToString(),
                            PRCID = reader["身份证号码"].ToString(),
                            SalesProduction = reader["融合促销包"].ToString(),
                            Phone = reader["联系电话"].ToString(),
                            IsFuse = reader["是否家庭融合宽带"].ToString() == "是"
                        };
                        DB.Records.Add(record);
                        var house = DB.Houses
                            .Include(x => x.Building)
                            .ThenInclude(x => x.Estate)
                            .ThenInclude(x => x.Rules)
                            .Where(x => x.Account == record.Account)
                            .SingleOrDefault();
                        if (house != null)
                        {
                            // 检查地址变更
                            var rules = house.Building.Estate.Rules
                                .Select(x => x.Rule)
                                .ToList();
                            var flag = false;
                            foreach(var x in rules)
                            {
                                if (record.ImplementAddress.Contains(x) || record.StandardAddress.Contains(x))
                                {
                                    flag = true;
                                    break;
                                }
                            }

                            // 如果地址变更则更新地址信息
                            if (!flag)
                            {
                                var estate = DB.EstateRules
                                    .Include(x => x.Estate)
                                    .Where(x => record.StandardAddress.Contains(x.Rule) || record.ImplementAddress.Contains(x.Rule))
                                    .Select(x => x.Estate)
                                    .FirstOrDefault();
                                if (estate != null)
                                {

                                }
                            }
                        }
                        DB.SaveChanges();
                    }
                }
                return View();
            }
        }
    }
}