namespace Nethermind.Store
{
    public static class StoreMetrics
    {
        public static long BlocksDbReads { get; set; }
        public static long BlocksDbWrites { get; set; }
        public static long BlockInfosDbReads { get; set; }
        public static long BlockInfosDbWrites { get; set; }
        public static long StateDbReads { get; set; }
        public static long StateDbWrites { get; set; }
        public static long StorageDbReads { get; set; }
        public static long StorageDbWrites { get; set; }
        public static long StateTreeReads { get; set; }
        public static long StateTreeWrites { get; set; }
        public static long StorageTreeReads { get; set; }
        public static long StorageTreeWrites { get; set; }
        public static long TreeNodeHashCalculations { get; set; }
        public static long TreeNodeRlpEncodings { get; set; }
        public static long TreeNodeRlpDecodings { get; set; }
    }
}