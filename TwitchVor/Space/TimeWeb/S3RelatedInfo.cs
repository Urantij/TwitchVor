using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3.Model;

namespace TwitchVor.Space.TimeWeb;

public class S3RelatedInfo
{
    public readonly string bucketName;
    public readonly string objectName;

    public readonly string uploadId;

    public int nextPartNumber = 1;

    public List<PartETag> etags = new();

    public S3RelatedInfo(string bucketName, string objectName, string uploadId)
    {
        this.bucketName = bucketName;
        this.objectName = objectName;
        this.uploadId = uploadId;
    }

    public void SetEtag(int partNumber, string value)
    {
        lock (etags)
        {
            etags.Add(new PartETag(partNumber, value));
        }
    }
}
