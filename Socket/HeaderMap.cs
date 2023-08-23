using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Coflnet.Sky.Commands
{
    public class HeaderMap : ITextMap
    {
        System.Collections.Specialized.NameValueCollection headers;
        public HeaderMap(System.Collections.Specialized.NameValueCollection headers)
        {
            this.headers = headers;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return headers.AllKeys.Select(h=>new KeyValuePair<string,string>(h,headers[h])).GetEnumerator();
        }

        public void Set(string key, string value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return headers.GetEnumerator();
        }
    }
}
