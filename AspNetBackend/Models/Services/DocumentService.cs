﻿using AspNetBackend.Models.Dtos;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Azure.Storage.Blobs;
using System.Configuration;
using AspNetBackend.Models.Utilities;

namespace AspNetBackend.Models.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly HttpContextBase _context;
        public DocumentService(HttpContextBase context)
        {
            _context = context;
        }

        /// <summary>
        /// 업로드한 pdf 파일 저장
        /// </summary>
        /// <param name="pdfDoc"></param>
        /// <returns></returns>
        public async Task<PdfDocSaveResult> SaveUploadedPdfDocAsync(HttpPostedFileBase pdfDoc)
        {
            try
            {
                // 로컬 저장소
                // 파일 저장
                //string pdfDocName = Path.GetFileName(pdfDoc.FileName);
                //// 저장 경로 설정
                //string docDirectoryPath = _context.Server.MapPath("~/uploads/docs");

                //if (!Directory.Exists(docDirectoryPath))
                //{
                //    Directory.CreateDirectory(docDirectoryPath);
                //}

                //string pdfDocPath = Path.Combine(docDirectoryPath, pdfDocName);
                ////System.Diagnostics.Debug.WriteLine("파일 저장 경로: " + pdfDocPath);

                //// 비동기적으로 파일 저장
                //using (var fileStream = new FileStream(pdfDocPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                //{
                //    await pdfDoc.InputStream.CopyToAsync(fileStream);
                //}

                //return new PdfDocSaveResult
                //{
                //    IsSuccess = true,
                //    PdfDocName = pdfDocName,
                //};

                // 1. 파일 해시 생성 (HashUtility 사용)
                string fileHash = HashUtility.GenerateFileHash(pdfDoc);

                // 2. 캐시 확인 (MemoryCacheHelper 사용)
                var cachedResult = MemoryCacheHelper.GetFromCache(fileHash);
                if (cachedResult != null)
                {
                    return new PdfDocSaveResult { IsSuccess = true, PdfDocName = cachedResult.ToString() };
                }

                // 3. 저장소에 파일 저장
                // 클라우드 저장소
                string connectionString = ConfigurationManager.AppSettings["AzureConnectionString"];
                string containerName = ConfigurationManager.AppSettings["AzureContainerName"];
                // 경로 포함하여 blobName 설정
                string blobName = Path.Combine("uploads", "docs", Path.GetFileName(pdfDoc.FileName)).Replace("\\", "/");

                // Blob 서비스 클라이언트 생성
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // 컨테이너가 없으면 생성
                await containerClient.CreateIfNotExistsAsync();

                // Blob 클라이언트 생성
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // 비동기적으로 파일 업로드
                using (var stream = pdfDoc.InputStream)
                {
                    await blobClient.UploadAsync(stream, overwrite: true);
                }

                // 4. 캐시에 저장 (MemoryCacheHelper 사용)
                MemoryCacheHelper.AddToCache(fileHash, blobName);

                return new PdfDocSaveResult
                {
                    IsSuccess = true,
                    PdfDocName = blobName
                };
            }
            catch (Exception ex)
            {
                return new PdfDocSaveResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                };
            }
        }

        /// <summary>
        /// 저장한 pdf 파일을 요약 요청
        /// </summary>
        /// <param name="publicKeyPem">요약한 내용을 암호화할 공개키</param>
        /// <param name="pdfDocName"></param>
        /// <returns></returns>
        public async Task<PdfDocSummarizedResult> SummarizePdfDocAsync(string publicKeyPem, string pdfDocName)
        {
            using (var client = new HttpClient())
            {
                // 타임아웃 설정 (예: 100초)
                client.Timeout = TimeSpan.FromSeconds(100);

                // pdf 경로 구성
                // 로컬 환경
                //string basePath = HttpContext.Current.Server.MapPath("~/uploads/docs");
                //string pdfDocPath = Path.Combine(basePath, pdfDocName);
                //System.Diagnostics.Debug.WriteLine("저장된 pdf 파일 경로: " + pdfDocPath);

                var fastApiUrl = "http://127.0.0.1:8000/api/summary-pdf";
                
                // 클라우드
                // var fastApiUrl = "/api/summary-pdf";

                // 요청을 위한 익명 클래스
                var requestData = new
                {
                    PdfDocPath = pdfDocName,
                    PublicKeyPem = publicKeyPem,
                };

                try
                {
                    var response = await client.PostAsJsonAsync(fastApiUrl, requestData);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var summarizedDocument = await response.Content.ReadAsAsync<PdfDocSummarizedResult>();
                        //System.Diagnostics.Debug.WriteLine("요약 내용이 저장된 파일 이름: " + summarizedDocument.ResultDocName);

                        return new PdfDocSummarizedResult
                        {
                            IsSuccess = true,
                            ResultDocName = summarizedDocument.ResultDocName.Trim('"'), // Trim quotes from JSON string
                            DecryptionKey = summarizedDocument.DecryptionKey,
                            EncryptionInitialState = summarizedDocument.EncryptionInitialState,
                            AuthTag = summarizedDocument.AuthTag,
                        };
                    }
                }
                catch (TaskCanceledException ex)
                {
                    System.Diagnostics.Debug.WriteLine("요청 시간 에러: " + ex.Message);
                }

                return new PdfDocSummarizedResult
                { 
                    IsSuccess = false, 
                    ResultDocName = null, 
                    DecryptionKey = null,
                    EncryptionInitialState = null,
                    AuthTag = null,
                };
            }
        }
    }
}