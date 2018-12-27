using System;
using System.Web;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using FlyBirdYoYo.Utilities;
using FlyBirdYoYo.DomainEntity.ViewModel;
using FlyBirdYoYo.Utilities.DEncrypt;
using FlyBirdYoYo.Utilities.Interface;
using FlyBirdYoYo.DomainEntity.Login;
using FlyBirdYoYo.Utilities.Logging;

namespace FlyBirdYoYo.BusinessServices.Authentication
{
    /// <summary>
    /// Authentication service
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        #region  �ֶ�

        /// <summary>
        /// �û����õļ���˽Կ ��ֵ��
        /// </summary>
        private const string EncryptionKeyName = "EncryptionKey";
        private const string AuthKey = "Authorization";

        /// <summary>
        /// ����û�δ���� ��ôʹ��Ĭ�ϵ�˽Կ
        /// </summary>
        private readonly string _defaultEncrytionKeyValue = StringExtension.DEFAULT_ENCRYPT_KEY;


        #endregion


        #region  ����




        private string _encryptionKeyValue;
        /// <summary>
        /// ���ݿ����û����õļ�����Կ
        /// </summary>
        public string EncryptionKeyValue
        {
            get
            {
                if (null == _encryptionKeyValue)
                {
                    //��ѯ��˽Կ
                    //if (null != dal_Setting)
                    //{
                    //    var setting = dal_Setting.GetElementsByCondition(x => x.Name == EncryptionKeyName).FirstOrDefault();
                    //    if (null != setting)
                    //    {
                    //        _encryptionKeyValue = setting.Value;
                    //    }
                    //}
                    if (string.IsNullOrEmpty(_encryptionKeyValue))
                    {
                        _encryptionKeyValue = this._defaultEncrytionKeyValue;
                    }
                }
                return _encryptionKeyValue;

            }
            set { _encryptionKeyValue = value; }
        }


        #endregion

        /// <summary>
        /// ���캯��
        /// </summary>
        public AuthenticationService()
        {

        }


        #region ��Ȩ


        /// <summary>
        /// ��Ȩ��¼�����ɼ���token
        /// </summary>
        /// <param name="model"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public bool Authentication(IBaseLoginViewModel model, out string token)
        {
            var result = false;
            token = string.Empty;

            //-----���ݲ�ͬ�ĵ�¼���͡�������Ȩ�ж�---------

            try
            {


                // ��֤ͨ����    ����Ʊ�� ������
                var userDto = AuthProviderFactory.Login((BaseLoginViewModel)model);

                if (null == userDto)
                {
                    throw new Exception("��֤��Ȩʧ�ܣ�δ����Ȩ���û���");
                }

                var ticket = this.GenerateAuthenticationTicket(userDto);
                var encryptedTicket = this.EncryptAuthenticationTicket(ticket);

                token = encryptedTicket;

            }
            catch (Exception ex)
            {

                throw ex;
            }

            result = true;
            return result;
        }

        #endregion


        #region �������Ļ�ȡ�û���Ϣ

        /// <summary>
        /// ����û��Ƿ�Ϊ�����û�
        /// </summary>
        /// <returns></returns>
        public bool CheckUserIsSystemAdminFromHttpContext()
        {
            LoginSystemAdminResultViewModel sysAdminUserDtoModel = null;

            try
            {
                //�ӵ�ǰ�������ȼ�����֤���û���Ϣ
                if (ApplicationContext.Current.IsSystemAdmin==true)
                {
                    return true;
                }
                //���� ֧�ִ� cookie ��ȡ
                string ticket = string.Empty;

                //1 ���Դ�Cookie��ȡ
                if (ApplicationContext.HttpContext.Current.Request.Cookies.ContainsKey(Contanst.Login_Cookie_SystemAdminUserInfo)
                    && ApplicationContext.HttpContext.Current.GetCookie(Contanst.Login_Cookie_SystemAdminUserInfo).IsNotEmpty())
                {
                    ticket = ApplicationContext.HttpContext.Current.GetCookie(Contanst.Login_Cookie_SystemAdminUserInfo);
                }


                if (ticket.IsNullOrEmpty())
                {
                    return false;
                }

                sysAdminUserDtoModel = ticket.FromJsonToObject<LoginSystemAdminResultViewModel>();
                if (null != sysAdminUserDtoModel)
                {
                    #region ��֤����ǩ��

                  
                    string deSign = string.Empty;
                    try
                    {
                        deSign = DESEncrypt.Decrypt(sysAdminUserDtoModel.Sign);
                    }
                    catch
                    { }
                    if (deSign.IsNullOrEmpty())
                    {
                        return false;
                    }
                    string[] arrSign = deSign.Split('|');
                    long timeSnamp = arrSign[0].ToLong();
                    int step = arrSign[1].ToInt();

                    //ʱ���֮��ļ�����ܹ���-���ɳ���8Сʱ
                    if ((DateTime.Now.ToTimeStampMilliseconds() - timeSnamp) / 1000 > 60 * 60 * 8)
                    {
                        return false;
                    }
                    #endregion

                    ApplicationContext.Current.IsSystemAdmin = sysAdminUserDtoModel.IsSuccess;
                }


                return ApplicationContext.Current.IsSystemAdmin;

            }
            catch (Exception ex)
            {
                throw ex;
            }




        }

        /// <summary>
        /// ����֤����Cookie�л�ȡ��¼�û���Ϣ
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public ILoginAuthedUserDTO GetAuthenticatedUserFromHttpContext()
        {
            LoginAuthedUserDTO userDtoModel = null;

            try
            {
                //�ӵ�ǰ�������ȼ�����֤���û���Ϣ
                if (null != ApplicationContext.Current.User && ApplicationContext.Current.User.Identity.IsAuthenticated)
                {
                    userDtoModel = ApplicationContext.Current.User as LoginAuthedUserDTO;
                }
                if (null != userDtoModel)
                {
                    return userDtoModel;
                }
                //���� ֧�ִ� cookie��Header��Form����ȡ
                string encryptedTicket = string.Empty;
                StringValues valuePair;

                //1 ���Դ�Cookie��ȡ
                if (ApplicationContext.HttpContext.Current.Request.Cookies.ContainsKey(Contanst.Login_Cookie_Client_Key)
                    && ApplicationContext.HttpContext.Current.GetCookie(Contanst.Login_Cookie_Client_Key).IsNotEmpty())
                {
                    encryptedTicket = ApplicationContext.HttpContext.Current.GetCookie(Contanst.Login_Cookie_Client_Key);
                }

                else if (true == ApplicationContext.HttpContext.Current.Request.Headers.TryGetValue(AuthKey, out valuePair))
                {
                    //2 ��ͷ����ȡ
                    encryptedTicket = valuePair[0].URLDecode().URLDecode();//Note:����ת��������ֹ���α���
                }
                else
                {
                    //3 ���Դ�Form����ȡ
                    encryptedTicket = ApplicationContext.HttpContext.Current.Request.GetForm<string>(AuthKey);
                }


                if (encryptedTicket.IsNullOrEmpty())
                {
                    return null;
                }
                //���ܵõ�ƾ��
                var ticket = this.DecryptAuthenticationTicket(encryptedTicket);
                //�Ƿ��û�--���ߵ�¼ƾ�ݹ��ڵ�
                if (null == ticket || null == ticket.User || ticket.Expired)
                {
                    return null;
                }

                userDtoModel = ticket.User;
                //ע���¼�û�����������Ϣ��
                var cardIdentity = new FlyBirdIdentity(userDtoModel.LoginType, true, userDtoModel.UserName);
                userDtoModel.SetIdentity(cardIdentity);

 
 

                #endregion

                ApplicationContext.Current.User = userDtoModel;

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return userDtoModel;


        }


     



        /// <summary>
        /// ������֤��Ʊ��
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        private string EncryptAuthenticationTicket(AuthenticationTicket ticket)
        {

            var jsonTicket = ticket.ToJson();
            return DESEncrypt.Encrypt(jsonTicket, this.EncryptionKeyValue);
        }

        /// <summary>
        /// �����ܵ�Ʊ��  ���ܴ���
        /// </summary>
        /// <param name="encryptedTicket"></param>
        /// <returns></returns>
        private AuthenticationTicket DecryptAuthenticationTicket(string encryptedTicket)
        {
            AuthenticationTicket ticket = null;

            try
            {
                //���ܳ���json
                var jsonTicket = DESEncrypt.Decrypt(encryptedTicket, this.EncryptionKeyValue);
                ticket = jsonTicket.FromJson<AuthenticationTicket>();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return ticket;
        }

        /// <summary>
        /// Ϊ�û�����һ����¼Ʊ��
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private AuthenticationTicket GenerateAuthenticationTicket(LoginAuthedUserDTO user)
        {
            if (null == user)
            {
                throw new Exception("�û�����Ϊ�գ�");

            }
            var now = DateTime.UtcNow.ToLocalTime();
            var expirationTime = ConfigHelper.AppSettingsConfiguration.GetConfigInt("signTimeOut");// FormsAuthentication.Timeout;
            if (expirationTime <= 0)
            {
                expirationTime = Contanst.Default_SignTimeOut;//����
            }
            var expirationTimeSpan = TimeSpan.FromMinutes(expirationTime);
            var ticket = new AuthenticationTicket() { User = user, Expiration = now.Add(expirationTimeSpan) };

            return ticket;
        }




    }
}