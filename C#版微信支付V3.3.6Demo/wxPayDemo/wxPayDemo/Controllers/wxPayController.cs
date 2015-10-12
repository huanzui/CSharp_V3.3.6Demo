using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using wxPayDemo.TenPayLibV3;
using Senparc.Weixin.MP.AdvancedAPIs;
using System.Xml.Linq;

namespace wxPayDemo.Controllers
{
    public class wxPayController : Controller
    {

        // 变量
        protected OAuthAccessTokenResult oAuthAccessTokenResult = new OAuthAccessTokenResult();
        protected string openid = string.Empty;
        protected string accessToken = string.Empty;
        //服务器异步通知页面路径
        private string NOTIFY_URL
        {
            get { return "http://www.自己的域名.com/wxPayDemo/WxPay/notice"; }
        }
        private TenPayV3Info _tenPayInfo;
        public TenPayV3Info TenPayInfo
        {
            get
            {
                if (_tenPayInfo == null)
                {
                    try
                    {
                        //将appId等替换自己的
                        _tenPayInfo = new TenPayV3Info("appId", "mchid", "wxkey", "appsecret", NOTIFY_URL);
                    }
                    catch { }
                }
                return _tenPayInfo;
            }
        }

        /// <summary>
        /// 授权认证
        /// </summary>
        public void weChatOauth(string code)
        {
            if (string.IsNullOrEmpty(this.openid) && string.IsNullOrEmpty(this.accessToken))
            {
                if (!string.IsNullOrEmpty(code))
                {
                    this.oAuthAccessTokenResult = OAuth.GetAccessToken("AppId", "AppSecret", code);
                    this.oAuthAccessTokenResult = OAuth.RefreshToken("AppId", this.oAuthAccessTokenResult.refresh_token);
                    this.openid = this.oAuthAccessTokenResult.openid;
                    this.accessToken = this.oAuthAccessTokenResult.access_token;
                }
                else
                {
                    string _url = OAuth.GetAuthorizeUrl("AppId", Request.Url.ToString(), "state", OAuthScope.snsapi_base);
                    Response.Redirect(_url);
                }
            }
        }

        public ActionResult Index()
        {
            string _code = Request.QueryString["code"];

            //微信授权（如需授权，可开通）
            //weChatOauth(_code);
            //this.oAuthAccessTokenResult = OAuth.GetAccessToken("AppId", "AppSecret", _code);
            //this.oAuthAccessTokenResult = OAuth.RefreshToken("AppId", this.oAuthAccessTokenResult.refresh_token);
            //this.openid = this.oAuthAccessTokenResult.openid;
            //this.accessToken = this.oAuthAccessTokenResult.access_token;

            string timeStamp = TenPayUtil.GetTimestamp();
            string nonceStr = TenPayUtil.GetNoncestr();
            string paySign = "";
            string sp_billno = Request["order_no"];
            string openid = this.openid;

            //附加数据
            string attach = sp_billno;
            //当前时间 yyyyMMdd
            string date = DateTime.Now.ToString("yyyyMMdd");

            if (null == sp_billno)
            {
                //生成订单10位序列号，此处用时间和随机数生成，商户根据自己调整，保证唯一
                sp_billno = DateTime.Now.ToString("HHmmss") + TenPayUtil.BuildRandomStr(28);
            }
            else
            {
                sp_billno = Request["order_no"].ToString();
            }


            //创建支付应答对象
            RequestHandler packageReqHandler = new RequestHandler(null);
            //初始化
            //packageReqHandler.Init();
            //packageReqHandler.SetKey(TenPayInfo.Key);
            //设置package订单参数
            packageReqHandler.SetParameter("appid", TenPayInfo.AppId);		  //公众账号ID
            packageReqHandler.SetParameter("body", "test");
            packageReqHandler.SetParameter("mch_id", TenPayInfo.Mchid);		  //商户号
            packageReqHandler.SetParameter("nonce_str", nonceStr.ToLower());                    //随机字符串
            packageReqHandler.SetParameter("notify_url", TenPayInfo.TenPayNotify);		    //接收财付通通知的URL
            packageReqHandler.SetParameter("openid", openid);	                    //openid
            packageReqHandler.SetParameter("out_trade_no", sp_billno);		//商家订单号
            packageReqHandler.SetParameter("spbill_create_ip", Request.UserHostAddress);   //用户的公网ip，不是商户服务器IP
            packageReqHandler.SetParameter("total_fee", "1");			        //商品金额,以分为单位(money * 100).ToString()
            packageReqHandler.SetParameter("trade_type", "JSAPI");	                    //交易类型

            //获取package包
            string sign = packageReqHandler.CreateMd5Sign("key", TenPayInfo.Key);
            packageReqHandler.SetParameter("sign", sign);	                    //交易类型
            string data = packageReqHandler.ParseXML();
            var result = TenPayV3.Unifiedorder(data);
            var res = XDocument.Parse(result);

            string prepayId = "";
            try
            {
                if (res.Element("xml").Element("return_code").Value == "SUCCESS")
                    prepayId = res.Element("xml").Element("prepay_id").Value;
            }
            catch (Exception ex)
            {
                return View();
            }
            string package = string.Format("prepay_id={0}", prepayId);
            timeStamp = TenPayUtil.GetTimestamp();

            //设置支付参数
            RequestHandler paySignReqHandler = new RequestHandler(null);
            paySignReqHandler.SetParameter("appId", TenPayInfo.AppId);
            paySignReqHandler.SetParameter("timeStamp", timeStamp);
            paySignReqHandler.SetParameter("nonceStr", nonceStr);
            paySignReqHandler.SetParameter("package", package);
            paySignReqHandler.SetParameter("signType", "MD5");
            paySign = paySignReqHandler.CreateMd5Sign("key", TenPayInfo.Key);

            ViewData["appId"] = TenPayInfo.AppId;
            ViewData["timeStamp"] = timeStamp;
            ViewData["nonceStr"] = nonceStr;
            ViewData["package"] = package;
            ViewData["paySign"] = paySign;

            return View();
        }

        //支付回调页面
        public ActionResult notice()
        {
            string resultFromWx = getPostStr();
            //设置支付参数
            RequestHandler paySignReqHandler = new RequestHandler(null);
            var res = XDocument.Parse(resultFromWx);
            //通信成功
            if (res.Element("xml").Element("return_code").Value == "SUCCESS")
            {
                if (res.Element("xml").Element("result_code").Value == "SUCCESS")
                {
                    //交易成功
                    paySignReqHandler.SetParameter("return_code", "SUCCESS");
                    paySignReqHandler.SetParameter("return_msg", "OK");

                    string ordecode = res.Element("xml").Element("out_trade_no").Value;
                    //处理定单到数据库///////////////////////////////////////////////////////
                    

                }
                else
                {
                    paySignReqHandler.SetParameter("return_code", "FAIL");
                    paySignReqHandler.SetParameter("return_msg", "交易失败");
                }
            }
            else
            {
                paySignReqHandler.SetParameter("return_code", "FAIL");
                paySignReqHandler.SetParameter("return_msg", "签名失败");
            }
            string data = paySignReqHandler.ParseXML();
            var result = TenPayV3.Unifiedorder(data);//发送给微信服务器

            return View();
        }

        //获得Post过来的数据
        public string getPostStr()
        {
            Int32 intLen = Convert.ToInt32(Request.InputStream.Length);
            byte[] b = new byte[intLen];
            Request.InputStream.Read(b, 0, intLen);
            return System.Text.Encoding.UTF8.GetString(b);
        }
    }
}
