﻿using Common.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Common.Proxy
{
    public class CAProxy : ChannelFactory<ICertificationAuthorityContract>, IDisposable
    {
        #region Fields

        private ICertificationAuthorityContract factory;

        private static string addressOfHotCAHost = null;
        private static string addressOfBackupCAHost = null;
        private static NetTcpBinding binding = null;

        private enum EnumCAServerState { BothOn = 0, OnlyActiveOn = 1, BothOff = 2 };
        private static EnumCAServerState CA_SERVER_STATE = EnumCAServerState.BothOn;
        private static string ACTIVE_SERVER_ADDRESS = null;
        private static string NON_ACTIVE_SERVER_ADDRESS = null;

        static CAProxy()
        {
            binding = new NetTcpBinding();
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            addressOfHotCAHost = "net.tcp://localhost:10000/CertificationAuthority";
            addressOfBackupCAHost = "net.tcp://localhost:10001/CertificationAuthorityBACKUP";
            ACTIVE_SERVER_ADDRESS = addressOfHotCAHost;
            NON_ACTIVE_SERVER_ADDRESS = addressOfBackupCAHost;
        }

        #endregion

        #region Constructor

        private CAProxy(NetTcpBinding binding, string address)
            : base(binding, address)
        {
            factory = this.CreateChannel();
        }

        #endregion

        #region Private methods

        private static void SwitchActiveNonActiveAddress()
        {
            string temp = NON_ACTIVE_SERVER_ADDRESS;
            NON_ACTIVE_SERVER_ADDRESS = ACTIVE_SERVER_ADDRESS;
            ACTIVE_SERVER_ADDRESS = temp;
        }

        private static bool IntegrityUpdate(CAProxy activeProxy, CAProxy nonActiveProxy)
        {
            //TODO: OSLOBODITI INTEGRITY UPDATE
            return false;

            bool retVal = false;
            CAModelDto objModel = null;

            objModel = activeProxy.factory.GetModel();
            retVal = nonActiveProxy.factory.SetModel(objModel);

            return retVal;
        }

        #endregion

        #region Public methods

        public static CertificateDto GenerateCertificate(string subject, string address)
        {
            CertificateDto retCertDto = null;
            X509Certificate2 certificate = null;

            try
            {
                //try communication with ACTIVE CA server
                using (CAProxy activeProxy = new CAProxy(binding, ACTIVE_SERVER_ADDRESS))
                {
                    retCertDto = activeProxy.factory.GenerateCertificate(subject, address);
                    certificate = retCertDto.GetCert();
                    if (certificate != null)
                    {
                        //FileStream certFileStream = activeProxy.factory.GetFileStreamOfCertificate(subject);
                        //TODO: obavezno pogledati kada zatvoriti ovaj filestream (na CAProxy-u ili na CAService-u)!!!!

                        #region try replication to NONACTIVE CA server
                        try
                        {
                            //replicate to NONACTIVE server
                            using (CAProxy nonActiveProxy = new CAProxy(binding, NON_ACTIVE_SERVER_ADDRESS))
                            {
                                if (CA_SERVER_STATE == EnumCAServerState.BothOn)
                                {
                                    //TODO: srediti REPLICIRANJE novokreiranog sertifikata na backup CA servis
                                    //nonActiveProxy.factory.SaveCertificateToBackupDisc(certificate, certFileStream, subject);
                                    //mozda ovde zatvoriti file stream
                                }
                                else if (CA_SERVER_STATE == EnumCAServerState.OnlyActiveOn)
                                {
                                    //nonActiveProxy.factory.INTEGRITY_UPDATE!!!
                                    IntegrityUpdate(activeProxy, nonActiveProxy);
                                    CA_SERVER_STATE = EnumCAServerState.BothOn;
                                }
                            }
                        }
                        catch (EndpointNotFoundException exNONACTIVE)
                        {
                            CA_SERVER_STATE = EnumCAServerState.OnlyActiveOn;
                        }
                        #endregion
                    }
                }
            }
            catch (EndpointNotFoundException exACTIVE)
            {
                try
                {
                    //try communication with NONACTIVE CA server
                    using (CAProxy backupProxy = new CAProxy(binding, NON_ACTIVE_SERVER_ADDRESS))
                    {
                        retCertDto = backupProxy.factory.GenerateCertificate(subject, address);
                        certificate = retCertDto.GetCert();

                        SwitchActiveNonActiveAddress();
                        CA_SERVER_STATE = EnumCAServerState.OnlyActiveOn;
                    }
                }
                catch (EndpointNotFoundException exNONACTIVE)
                {
                    Console.WriteLine("Both of CA servers not working!");
                    CA_SERVER_STATE = EnumCAServerState.BothOff;
                    return retCertDto;
                }

            }

            return retCertDto;
        }


        public bool WithdrawCertificate(X509Certificate2 certificate)
        {
            throw new NotImplementedException();
        }

        public static bool IsCertificateActive(X509Certificate2 certificate)
        {
            bool retValue = false;

            try
            {
                //try communication with ACTIVE CA server
                using (CAProxy activeProxy = new CAProxy(binding, ACTIVE_SERVER_ADDRESS))
                {
                    retValue = activeProxy.factory.IsCertificateActive(certificate);
                }
            }
            catch (EndpointNotFoundException exACTIVE)
            {
                try
                {
                    //try communication with NONACTIVE CA server
                    using (CAProxy nonActiveProxy = new CAProxy(binding, NON_ACTIVE_SERVER_ADDRESS))
                    {
                        retValue = nonActiveProxy.factory.IsCertificateActive(certificate);

                        SwitchActiveNonActiveAddress();
                        CA_SERVER_STATE = EnumCAServerState.OnlyActiveOn;
                    }
                }
                catch (EndpointNotFoundException exNONACTIVE)
                {
                    Console.WriteLine("Both of CA servers not working!");
                    CA_SERVER_STATE = EnumCAServerState.BothOff;
                    return retValue;
                }

            }

            return retValue;
        }

        #endregion

        #region IDisposable methods

        public void Dispose()
        {
            if(factory != null)
            {
                factory = null;
            }

            this.Abort();   //*********************************** OBAVEZNO, INACE BACA CommunicationObjectFaultedException

            this.Close();
        }

        #endregion        
    }
}
