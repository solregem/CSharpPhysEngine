﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace PhysEngine
{
    struct RayHitInfo
    {
        public RayHitInfo( Vector ptHit, Vector vNormal, BaseEntity HitEnt )
        {
            this.bHit = true;
            this.ptHit = ptHit;
            this.vNormal = vNormal;
            this.HitEnt = HitEnt;
        }
        public bool bHit;
        public Vector ptHit;
        public Vector vNormal;
        public BaseEntity HitEnt;
    }
    class World : BaseWorld
    {
        public World( Vector Gravity, float PhysSimTime ) : base()
        {
            Environment = new( Gravity );
            Simulator = new( PhysSimTime, Environment, this );
        }

        public PhysicsEnvironment Environment;
        public PhysicsSimulator Simulator;

        private Player _Player;
        public Player player
        { 
            get => _Player; 
            set
            {
                _Player = value;
                if ( !PhysicsObjects.Contains( _Player.Body ) )
                    PhysicsObjects.Add( _Player.Body );
                if ( !WorldEnts.Contains( _Player.camera ) )
                    WorldEnts.Add( _Player.camera );
                if ( !Environment.PObjs.Contains( _Player.Body ) )
                    Environment.PObjs.Add( _Player.Body );
            } 
        }

        public void Add( params BaseEntity[] ent )
        {
            WorldEnts.AddRange( ent );
        }
        public void Add( params PhysObj[] pobjs )
        {
            foreach ( PhysObj p in pobjs )
            {
                if ( !WorldEnts.Contains( p.LinkedEnt ) )
                {
                    WorldEnts.Add( p.LinkedEnt );
                }
            }
            PhysicsObjects.AddRange( pobjs );
            Environment.PObjs.AddRange( pobjs );
        }
        public void Close()
        {
            for ( int i = 0; i < WorldEnts.Count; ++i )
                WorldEnts[ i ].Close();
        }

        public RayHitInfo TraceRay( Vector ptStart, Vector ptEnd, params BaseEntity[] IgnoreEnts )
        {
            int TraceFidelity = 300;
            Vector vDirection = ( ptEnd - ptStart ) / TraceFidelity;
            while ( TraceFidelity > 0 )
            {
                for ( int i = 0; i < WorldEnts.Count; ++i )
                {
                    if ( IgnoreEnts.Contains( WorldEnts[ i ] ) )
                        continue;

                    if ( WorldEnts[ i ].TestCollision( ptStart ) )
                    {
                        Plane plane = WorldEnts[ i ].GetCollisionPlane( ptStart );
                        return new RayHitInfo( ptStart, plane.Normal, WorldEnts[ i ] );
                    }
                }
                ptStart += vDirection;
                --TraceFidelity;
            }
            return new RayHitInfo();
        }

        /*
        public void ToFile( string filepath )
        {
            FileStream fs = new FileStream( filepath, FileMode.Create );
            BinaryWriter bw = new BinaryWriter( fs );
            BaseEntity[] entlist = GetEntList();

            //player indexes (to point player to in reconstruction)
            int PlayerHeadIndex = WorldEnts.IndexOf( player.Head );
            bw.Write( PlayerHeadIndex );
            int PlayerBodyIndex = PhysicsObjects.IndexOf( player.Body );
            bw.Write( PlayerBodyIndex );

            //save the names of the textures in this world
            bw.Write( Textures.Count );
            for ( int i = 0; i < Textures.Count; ++i )
                bw.Write( Textures[ i ].TextureName );

            //tell the file how long the entlist is
            bw.Write( entlist.Length );
            for ( int i = 0; i < entlist.Length; ++i )
            {
                bw.Write( WorldEnts.IndexOf( WorldEnts[ i ].Parent ) ); //write the world index of the parent to the file

                bw.Write( WorldEnts[ i ].ent.FaceLength );
                for ( int f = 0; f < WorldEnts[i].ent.FaceLength; ++f )
                {
                    int TexIndex = TextureIndex( WorldEnts[ i ].ent.EntFaces[ f ].texture.ID );
                    bw.Write( TexIndex != -1 );
                    if ( TexIndex != -1 )
                        bw.Write( Textures[ TexIndex ].TextureName );

                    int facesize = Marshal.SizeOf( typeof( BaseFace ) );
                    byte[] facebytes = new byte[ facesize ];
                    IntPtr faceptr = Marshal.AllocHGlobal( facesize );
                    Marshal.StructureToPtr( entlist[ i ].EntFaces[ f ], faceptr, false );
                    Marshal.Copy( faceptr, facebytes, 0, facesize );
                    Marshal.FreeHGlobal( faceptr );
                    bw.Write( facebytes );
                }

                int entsize = Marshal.SizeOf( typeof( BaseEntity ) );
                byte[] bytes = new byte[ entsize ];
                IntPtr ptr = Marshal.AllocHGlobal( entsize );
                Marshal.StructureToPtr( entlist[ i ], ptr, true );
                Marshal.Copy( ptr, bytes, 0, entsize );
                Marshal.FreeHGlobal( ptr );
                bw.Write( bytes );
            }
            //write physics objects to file
            bw.Write( PhysicsObjects.Count );
            for ( int i = 0; i < PhysicsObjects.Count; ++i )
            {
                bw.Write( WorldEnts.IndexOf( PhysicsObjects[ i ].LinkedEnt ) );
                for ( int j = 0; j < 3; ++j )
                {
                    bw.Write( PhysicsObjects[ i ].Gravity[ j ] );
                    bw.Write( PhysicsObjects[ i ].AirDragCoeffs[ j ] );
                    bw.Write( PhysicsObjects[ i ].Velocity[ j ] );
                    bw.Write( PhysicsObjects[ i ].BaseVelocity[ j ] );
                }
                bw.Write( PhysicsObjects[ i ].Mass );
                bw.Write( PhysicsObjects[ i ].RotInertia );
            }
            fs.Close();
            bw.Close();
        }
        //reads the data from a file to construct a world
        public static World FromFile( string filepath )
        {
            StreamReader sr = new StreamReader( filepath );
            BinaryReader br = new BinaryReader( sr.BaseStream );

            World w = new World();

            int PlayerHeadIndex = br.ReadInt32();
            int PlayerBodyIndex = br.ReadInt32();

            Player p = new Player( Matrix.IdentityMatrix(), PhysicsObject.Default_Gravity, PhysicsObject.Default_Coeffs, Player.PLAYER_MASS, Player.PLAYER_ROTI );

            //get all the textures used in the world for reconstruction
            int TexLength = br.ReadInt32();
            for ( int i = 0; i < TexLength; ++i )
                w.Textures.Add( new TextureHandle( br.ReadString() ) );

            //get how many entities are in this world
            int size = br.ReadInt32();
            BaseEntity[] entlist = new BaseEntity[ size ];
            //can't do it during the loop, because if it has a parent for one at a later index it hasn't been init'd
            int[] ParentIndexes = new int[ size ];

            for ( int i = 0; i < size; ++i )
            {
                ParentIndexes[ i ] = br.ReadInt32();

                int FaceSize = br.ReadInt32();
                BaseFace[] Faces = new BaseFace[ FaceSize ];
                for ( int f = 0; f < FaceSize; ++f )
                {
                    string TextureName = "";
                    if ( br.ReadBoolean() )
                        TextureName = br.ReadString();

                    byte[] facebytes = br.ReadBytes( Marshal.SizeOf( typeof( BaseFace ) ) );
                    GCHandle facehandle = GCHandle.Alloc( facebytes, GCHandleType.Pinned );
                    Faces[ f ] = (BaseFace) Marshal.PtrToStructure( facehandle.AddrOfPinnedObject(), typeof( BaseFace ) );
                    facehandle.Free();
                    //we need to call the constructor to init in native code
                    if ( TextureName != "" )
                        Faces[ f ] = new BaseFace( Faces[ f ].Verts, Faces[ f ].VertLength, Faces[ f ].Inds, Faces[ f ].IndLength, w.Textures[ w.TextureIndex( TextureName ) ].texture, Faces[ f ].Normal );
                    else
                        Faces[ f ] = new BaseFace( Faces[ f ].Verts, Faces[ f ].VertLength, Faces[ f ].Inds, Faces[ f ].IndLength, new Texture(), Faces[ f ].Normal );
                }

                byte[] bytes = br.ReadBytes( Marshal.SizeOf( typeof( BaseEntity ) ) );
                GCHandle handle = GCHandle.Alloc( bytes, GCHandleType.Pinned );
                entlist[ i ] = (BaseEntity) Marshal.PtrToStructure( handle.AddrOfPinnedObject(), typeof( BaseEntity ) );
                handle.Free();
                //we need to call the constructor to init in native code
                entlist[ i ] = new BaseEntity( Faces, entlist[ i ].transform, entlist[ i ].AABB.mins, entlist[ i ].AABB.maxs );
            }

            EHandle[] handles = new EHandle[ entlist.Length ];
            for ( int i = 0; i < entlist.Length; ++i )
            {
                handles[ i ] = new EHandle( entlist[ i ] )
                {
                    Transform = new THandle( entlist[ i ].transform )
                };
            }
            for ( int i = 0; i < entlist.Length; ++i )
            {
                if ( ParentIndexes[ i ] != -1 )
                    handles[ i ].Parent = handles[ ParentIndexes[ i ] ];
            }

            //reconstruct physics objects
            int PhysObjCount = br.ReadInt32();
            PhysicsObject[] pObjs = new PhysicsObject[ PhysObjCount ];
            for ( int i = 0; i < PhysObjCount; ++i )
            {
                int EntIndex = br.ReadInt32();
                Vector Gravity, AirDragCoeffs, Velocity, BaseVelocity;
                Gravity = AirDragCoeffs = Velocity = BaseVelocity = new Vector();
                for ( int j = 0; j < 3; ++j )
                {
                    Gravity[ j ] = br.ReadSingle();
                    AirDragCoeffs[ j ] = br.ReadSingle();
                    Velocity[ j ] = br.ReadSingle();
                    BaseVelocity[ j ] = br.ReadSingle();
                }
                float Mass = br.ReadSingle();
                float RotInertia = br.ReadSingle();
                pObjs[ i ] = new PhysicsObject( handles[ EntIndex ], Gravity, AirDragCoeffs, Mass, RotInertia )
                {
                    Velocity = Velocity,
                    BaseVelocity = BaseVelocity
                };
            }

            w.Add( handles );
            w.Add( false, pObjs );

            p.Head = handles[ PlayerHeadIndex ];
            p.Body = pObjs[ PlayerBodyIndex ];
            p.Head.Parent = p.Body.LinkedEnt;
            p.Head.Transform.SetLocalPos( Player.EYE_CENTER_OFFSET );

            w.player = p;

            sr.Close();
            br.Close();

            return w;
        }
        */
    }
}
