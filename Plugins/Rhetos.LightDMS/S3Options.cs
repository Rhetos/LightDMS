﻿/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

namespace Rhetos.LightDMS
{
    [Options("Rhetos:LightDMS:S3")]
    public class S3Options
    {
        public string Key { get; set; }

        public string AccessKeyID { get; set; }

        public string ServiceURL { get; set; }

        public string BucketName { get; internal set; }

        public string DestinationFolder { get; internal set; }

        public string CertificateSubject { get; internal set; }

        public bool ForcePathStyle { get; set; }
    }
}
