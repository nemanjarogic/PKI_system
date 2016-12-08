﻿using Common.Server;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace CertificationAuthority
{
    public class CertificationAuthorityService : ICertificationAuthorityContract
    {
        #region Fields

        private HashSet<X509Certificate2> activeCertificates;
        private HashSet<X509Certificate2> revocationList;
        private AsymmetricKeyParameter caPrivateKey = null;
        private X509Certificate2 caCertificate = null;

        private readonly string CA_SUBJECT_NAME;
        private readonly string PFX_PATH;
        private readonly string PFX_PASSWORD;
        private readonly string CERT_FOLDER_PATH;

        #endregion

        #region Constructor

        public CertificationAuthorityService()
        {
            activeCertificates = new HashSet<X509Certificate2>();
            revocationList = new HashSet<X509Certificate2>();

            CA_SUBJECT_NAME = "CN=PKI_CA";
            PFX_PATH = @"..\..\SecurityStore\PKI_CA.pfx";
            PFX_PASSWORD = "123";
            CERT_FOLDER_PATH = @"..\..\SecurityStore\";

            PrepareCAService();
        }

        #endregion

        #region Public methods

        public X509Certificate2 GenerateCertificate(string subjectName)
        {
            return CertificateHandler.GenerateAuthorizeSignedCertificate(subjectName, "CN=" + CA_SUBJECT_NAME, caPrivateKey);
        }

        public bool WithdrawCertificate(X509Certificate2 certificate)
        {
            throw new NotImplementedException();
        }

        public bool IsCertificateActive(X509Certificate2 certificate)
        {
            return true;
        }

        public FileStream GetFileStreamOfCertificate(string certFileName)
        {
            return new FileStream(CERT_FOLDER_PATH + @"\" + certFileName + ".cer", FileMode.Open, FileAccess.Read);
        }

        public bool SaveCertificateToBackupDisc(X509Certificate2 certificate, FileStream stream, string certFileName)
        {
            //save file to disk
            var fileStream = File.Create(CERT_FOLDER_PATH + @"\" + certFileName + ".cer");
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(fileStream);
            fileStream.Close();

            //add cert to list
            activeCertificates.Add(certificate);

            return true;
        }

        #endregion 

        #region Private methods

        private void PrepareCAService()
        {
            bool isPfxCreated = true;
            bool isCertFound = false;
            X509Certificate2Collection collection = new X509Certificate2Collection();

            try
            {
                collection.Import(PFX_PATH, PFX_PASSWORD, X509KeyStorageFlags.PersistKeySet);
            }
            catch
            {
                isPfxCreated = false;
            }

            if(isPfxCreated)
            {
                foreach (X509Certificate2 cert in collection)
                {
                    if (cert.SubjectName.Name.Equals(CA_SUBJECT_NAME))
                    {
                        isCertFound = true;
                        caCertificate = cert;
                        caPrivateKey = DotNetUtilities.GetKeyPair(cert.PrivateKey).Private;
                        break;
                    }
                }
            }

            if (!isCertFound)
            {
                caCertificate = CertificateHandler.GenerateCACertificate(CA_SUBJECT_NAME, ref caPrivateKey);
            }
        }

        #endregion 
    }
}