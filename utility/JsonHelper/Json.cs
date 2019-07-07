using System;

namespace NetHpServer.utility.JsonHelper
{
    public static class JsonExtend
    {
        /// <summary>
        /// 将对像转换为JSON字符串
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string JsonConvert(this object o)
        {
            Newtonsoft.Json.Converters.IsoDateTimeConverter timeConverter =
                new Newtonsoft.Json.Converters.IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff" };
            return Newtonsoft.Json.JsonConvert.SerializeObject(o, Newtonsoft.Json.Formatting.Indented, timeConverter);

        }

        /// <summary>
        /// 序列化obj，自动对齐
        /// </summary>
        /// <param name="o">对象</param>
        /// <param name="isIndented">true对齐，false不对齐</param>
        /// <returns></returns>
        public static string JsonConvert(this object o, bool isIndented)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(o, isIndented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
        }

        /// <summary>
        /// 将JSON字符串转换为对像
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        public static T JsonConvert<T>(this string str)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(str);
        }

        /// <summary>
        /// 将字符串反序列化成特定对象
        /// </summary>
        /// <param name="str"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object JsonConvert(this string str, Type type)
        {

            return Newtonsoft.Json.JsonConvert.DeserializeObject(str, type);
        }

        /// <summary>
        /// 字符串反序列化成T类型对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <param name="obj">传递的对象</param>
        /// <returns></returns>
        public static T JsonConvert<T>(this string str, T obj)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeAnonymousType<T>(str, obj);

        }
    }
}